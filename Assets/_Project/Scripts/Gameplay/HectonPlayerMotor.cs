using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.Physics;
using Hecton8.World;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Authoritative kinematic application layer for player locomotion.
    /// All Rigidbody writes route through this component.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player/Hecton Player Motor")]
    public sealed class HectonPlayerMotor : MonoBehaviour, IMotorForces, IPostFixedTickable, ILateFrameTickable, IInventoryEventListener
    {
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct ScheduledSweepState
        {
            public Vector3 StartPosition;
            public Vector3 CapsulePoint1;
            public Vector3 CapsulePoint2;
            public float CapsuleRadius;
            public Vector3 Direction;
            public float Distance;
            public int LayerMask;
            public int SelfColliderInstanceId;
            public float SkinWidth;
        }

        private const float MinVectorMagnitudeSq = 0.000001f;
        private const int MaxSlideSweepIterations = 2;
        private const int ScheduledLocomotionSweepCommandIndex = 0;
        private const int ScheduledFootstepProbeCommandIndex = 1;
        private const int ScheduledSweepCommandCount = 2;
        private const int ScheduledSweepMaxHitsPerCommand = 8;
        private const int ScheduledSweepMaxHits = ScheduledSweepCommandCount * ScheduledSweepMaxHitsPerCommand;
        private const int KinematicRepairTargetCommandCount = 1;
        private const int KinematicRepairTargetResultCount = 1;
        private const int KinematicRepairTargetMinCommandsPerJob = 1;
        private const float ScheduledFootstepProbeDistance = 0.9f;
        private const float ScheduledFootstepProbeMinSupportNormalY = 0.12f;
        private const float ScheduledFootstepProbeMinSupportNormalYSq = ScheduledFootstepProbeMinSupportNormalY * ScheduledFootstepProbeMinSupportNormalY;
        private const float KinematicRepairTargetMinDistance = 0.05f;
        private const float DenormalVelocityFlushThresholdMetersPerSecond = 0.001f;
        private const float InventoryLoadMinimumMovementMultiplier = 0.62f;
        private const float WakeSiltEmissionSpeedThresholdMetersPerSecond = 4.5f;
        private const float WakeSiltEmissionCooldownSeconds = 0.35f;
        private const float WallSlideTelemetryMaxNormalY = 0.75f;
        private const float VoxelProxySlideDistanceRetain = 0.92f;
        private const float VoxelProxySlideVelocityRetain = 0.65f;
        private const float VoxelProxyGlideFallbackMetersPerSecond = 0.35f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const double SweepSolveTelemetryBudgetMilliseconds = 0.2d;
#endif

        private static readonly ProfilerMarker _scheduledSweepProfilerMarker = new ProfilerMarker("H8.PlayerMotor.CapsuleSweep.Schedule");
        private static readonly ProfilerMarker _scheduledSweepConsumeProfilerMarker = new ProfilerMarker("H8.PlayerMotor.CapsuleSweep.Consume");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly uint _sweepScheduleBudgetWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HectonPlayerMotor.CapsuleSweep.ScheduleOverBudget"));
        private static readonly uint _sweepConsumeBudgetWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HectonPlayerMotor.CapsuleSweep.ConsumeOverBudget"));
        private static readonly uint _sweepTelemetryContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("HectonPlayerMotor.CapsuleSweep"));
#endif

        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private PlayerInventory _encumbranceSource;
        private bool _isGrounded;
        private HectonPlayerMotorNativeState _nativeState;
        private bool _scheduledSweepPending;
        private bool _scheduledSweepResultReady;
        private bool _scheduledSweepWasBlocked;
        private ScheduledSweepState _scheduledSweepState;
        private RaycastHit _scheduledSweepBlockingHit;
        private Vector3 _scheduledSweepResolvedPosition;
        private float _scheduledSweepBlockedSpeed;
        private bool _registeredLateFrameTick;
        private bool _registeredPostFixedTick;
        private bool _registeredMotorService;
        private float _encumbranceMovementMultiplier = 1f;
        private float _wakeSiltEmissionCooldown;
        private Vector3 _lastWallSlideNormal;
        private Vector3 _lastWallSlidePoint;
        private float _lastWallSlideBlockedSpeed;
        private float _lastWallSlideAngleDegrees;
        private float _lastWallSlideVelocityReduction01;
        private int _lastWallSlidePhysicsFrame = -1;
        private uint _lastWallSlideShiftSequence;
        private uint _lastWallSlideBodyBindEpoch;
        private bool _lastWallSlideIsVoxel;
        private RaycastHit _lastBatchedFootstepHit;
        private int _lastBatchedFootstepPhysicsFrame = -1;
        private uint _lastBatchedFootstepShiftSequence;
        private uint _lastBatchedFootstepBodyBindEpoch;
        private bool _kinematicRepairTargetProbePending;
        private bool _kinematicRepairSnapReady;
        private uint _bodyBindEpoch;
        private uint _scheduledSweepBodyBindEpoch;
        private uint _scheduledSweepShiftSequence;
        private uint _kinematicRepairTargetBodyBindEpoch;
        private uint _kinematicRepairTargetShiftSequence;
        private uint _kinematicRepairSnapBodyBindEpoch;
        private uint _kinematicRepairSnapShiftSequence;
        private float _kinematicRepairProbeSurfaceOffset;
        private AbsoluteUniversePosition _kinematicRepairProbeOriginAup;
        private KinematicRepairTargetProbe _kinematicRepairTargetProbe;
        private KinematicRepairSnapPoint _kinematicRepairSnapPoint;

        /// <inheritdoc />
        public Rigidbody Body => _body;

        /// <inheritdoc />
        public CapsuleCollider Capsule => _capsule;

        /// <inheritdoc />
        public bool IsGrounded => _isGrounded;

        /// <summary>Current event-driven carry-load movement scalar.</summary>
        public float EncumbranceMovementMultiplier => _encumbranceMovementMultiplier;

        /// <summary>Returns the most recent KCC wall projection contact if it is still within the requested fixed-frame window.</summary>
        public bool TryGetRecentWallSlideContact(
            int maxPhysicsFrameAge,
            out Vector3 normal,
            out Vector3 point,
            out float blockedSpeed,
            out float slideAngleDegrees,
            out float velocityReduction01,
            out int physicsFrame)
        {
            return TryGetRecentWallSlideContact(
                maxPhysicsFrameAge,
                out normal,
                out point,
                out blockedSpeed,
                out slideAngleDegrees,
                out velocityReduction01,
                out physicsFrame,
                out _);
        }

        /// <summary>Returns the most recent KCC wall projection contact with voxel-wall classification.</summary>
        public bool TryGetRecentWallSlideContact(
            int maxPhysicsFrameAge,
            out Vector3 normal,
            out Vector3 point,
            out float blockedSpeed,
            out float slideAngleDegrees,
            out float velocityReduction01,
            out int physicsFrame,
            out bool isVoxelWall)
        {
            normal = Vector3.zero;
            point = Vector3.zero;
            blockedSpeed = 0f;
            slideAngleDegrees = 0f;
            velocityReduction01 = 0f;
            physicsFrame = _lastWallSlidePhysicsFrame;
            isVoxelWall = false;

            if (HectonFloatingOrigin.IsShiftInProgress)
                return false;

            if (_lastWallSlideShiftSequence != HectonFloatingOrigin.CurrentShiftSequence)
                return false;

            if (_lastWallSlideBodyBindEpoch != _bodyBindEpoch)
                return false;

            if (_lastWallSlidePhysicsFrame < 0)
                return false;

            int age = PhysicsFrame.Current - _lastWallSlidePhysicsFrame;
            if (age < 0 || age > math.max(0, maxPhysicsFrameAge))
                return false;

            if (_lastWallSlideNormal.sqrMagnitude <= MinVectorMagnitudeSq)
                return false;

            normal = _lastWallSlideNormal;
            point = _lastWallSlidePoint;
            blockedSpeed = _lastWallSlideBlockedSpeed;
            slideAngleDegrees = _lastWallSlideAngleDegrees;
            velocityReduction01 = _lastWallSlideVelocityReduction01;
            isVoxelWall = _lastWallSlideIsVoxel;
            return true;
        }

        /// <summary>Returns the latest support hit produced by the locomotion capsulecast batch.</summary>
        public bool TryGetRecentBatchedFootstepHit(int maxPhysicsFrameAge, out RaycastHit hit)
        {
            hit = default;
            if (HectonFloatingOrigin.IsShiftInProgress)
                return false;

            if (_lastBatchedFootstepShiftSequence != HectonFloatingOrigin.CurrentShiftSequence)
                return false;

            if (_lastBatchedFootstepBodyBindEpoch != _bodyBindEpoch)
                return false;

            if (_lastBatchedFootstepPhysicsFrame < 0)
                return false;

            int age = PhysicsFrame.Current - _lastBatchedFootstepPhysicsFrame;
            if (age < 0 || age > math.max(0, maxPhysicsFrameAge))
                return false;

            if (GetHitColliderInstanceId(in _lastBatchedFootstepHit) == 0)
                return false;

            if (!IsValidSupportFootstepHit(in _lastBatchedFootstepHit))
                return false;

            hit = _lastBatchedFootstepHit;
            return true;
        }

        /// <summary>Consumes the most recent hand IK repair snap resolved by the Burst raycast lane.</summary>
        public bool TryConsumeKinematicRepairSnap(out KinematicRepairSnapPoint snapPoint)
        {
            return TryConsumeKinematicRepairSnap(out _, out snapPoint);
        }

        /// <summary>Consumes the most recent repair probe and snap point pair.</summary>
        public bool TryConsumeKinematicRepairSnap(
            out KinematicRepairTargetProbe probe,
            out KinematicRepairSnapPoint snapPoint)
        {
            probe = default;
            snapPoint = default;
            if (HectonFloatingOrigin.IsShiftInProgress)
            {
                _kinematicRepairSnapReady = false;
                _kinematicRepairTargetProbe = default;
                _kinematicRepairSnapPoint = default;
                return false;
            }

            if (_kinematicRepairSnapShiftSequence != HectonFloatingOrigin.CurrentShiftSequence)
            {
                _kinematicRepairSnapReady = false;
                _kinematicRepairTargetProbe = default;
                _kinematicRepairSnapPoint = default;
                return false;
            }

            if (_kinematicRepairSnapBodyBindEpoch != _bodyBindEpoch)
            {
                _kinematicRepairSnapReady = false;
                _kinematicRepairTargetProbe = default;
                _kinematicRepairSnapPoint = default;
                return false;
            }

            if (!_kinematicRepairSnapReady)
                return false;

            _kinematicRepairSnapReady = false;
            probe = _kinematicRepairTargetProbe;
            snapPoint = _kinematicRepairSnapPoint;
            return snapPoint.ColliderInstanceId != 0;
        }

        /// <summary>Binds authoritative body references owned by the locomotion controller.</summary>
        public void Bind(Rigidbody body, CapsuleCollider capsule)
        {
            bool bodyChanged = _body != body || _capsule != capsule;
            _body = body;
            _capsule = capsule;
            if (bodyChanged)
            {
                unchecked
                {
                    _bodyBindEpoch++;
                }

                ResetBodyBoundCachedResults();
            }

            ResetHydrodynamicAddedMassState();
        }

        /// <summary>Binds the inventory source accepted by encumbrance events.</summary>
        public void BindEncumbranceSource(PlayerInventory inventory)
        {
            _encumbranceSource = inventory;
        }

        private void OnEnable()
        {
            InventoryEvents.Register(this);
            TryRegisterLateFrameTick();
            TryRegisterPostFixedTick();
            TryRegisterMotorService();
        }

        private void OnDisable()
        {
            InventoryEvents.Unregister(this);
            TryUnregisterLateFrameTick();
            TryUnregisterPostFixedTick();
            TryUnregisterMotorService();
            ResetWallSlideContactState();
            DisposeScheduledSweepState();
            ResetHydrodynamicAddedMassState();
        }

        private void OnDestroy()
        {
            InventoryEvents.Unregister(this);
            TryUnregisterLateFrameTick();
            TryUnregisterPostFixedTick();
            TryUnregisterMotorService();
            ResetWallSlideContactState();
            DisposeScheduledSweepState();
            ResetHydrodynamicAddedMassState();
        }

        /// <summary>Updates grounded state mirror for external systems.</summary>
        public void SetGroundedState(bool isGrounded)
        {
            _isGrounded = isGrounded;
        }

        /// <summary>Applies a pre-resolved carry-load movement scalar.</summary>
        public void SetEncumbranceMovementMultiplier(float multiplier)
        {
            _encumbranceMovementMultiplier = math.clamp(multiplier, InventoryLoadMinimumMovementMultiplier, 1f);
        }

        /// <inheritdoc />
        public void OnInventoryEvent(in InventoryEventPayload payload)
        {
            if ((InventoryEventType)payload.EventType != InventoryEventType.EncumbranceChanged)
                return;

            if (!InventoryEvents.TryBuildEncumbranceChangedEvent(in payload, out EncumbranceChangedEvent encumbranceEvent))
                return;

            HandleEncumbranceChanged(encumbranceEvent);
        }

        private void HandleEncumbranceChanged(EncumbranceChangedEvent payload)
        {
            if (_encumbranceSource != null && payload.Inventory != _encumbranceSource)
                return;

            float load01 = math.saturate(payload.Load01);
            SetEncumbranceMovementMultiplier(math.lerp(1f, InventoryLoadMinimumMovementMultiplier, load01));
        }

        /// <inheritdoc />
        public void AddExternalAcceleration(Vector3 acceleration)
        {
            ApplyAcceleration(acceleration);
        }

        /// <inheritdoc />
        public void AddExternalVelocityChange(Vector3 velocityChange)
        {
            ApplyVelocityChange(velocityChange);
        }

        /// <summary>Applies a world-space force through ForceMode.Force after finite validation.</summary>
        public void ApplyForce(Vector3 force)
        {
            if (_body == null || !IsFiniteNonZero(force))
                return;

            PhysicsForceRouter.QueueForce(_body, force, ForceMode.Force);
        }

        /// <summary>Applies a world-space acceleration after finite validation.</summary>
        public void ApplyAcceleration(Vector3 acceleration)
        {
            if (_body == null || !IsFiniteNonZero(acceleration))
                return;

            PhysicsForceRouter.QueueForce(_body, acceleration, ForceMode.Acceleration);
        }

        /// <summary>Applies a world-space velocity change after finite validation.</summary>
        public void ApplyVelocityChange(Vector3 velocityChange)
        {
            if (_body == null || !IsFiniteNonZero(velocityChange))
                return;

            PhysicsForceRouter.QueueForce(_body, velocityChange, ForceMode.VelocityChange);
        }

        /// <summary>Applies an impulse after finite validation.</summary>
        public void ApplyImpulse(Vector3 impulse)
        {
            if (_body == null || !IsFiniteNonZero(impulse))
                return;

            PhysicsForceRouter.QueueForce(_body, impulse, ForceMode.Impulse);
        }

        /// <summary>
        /// Applies torque using ForceMode.Force after clamping the resulting angular acceleration.
        /// </summary>
        public void ApplyTorque(Vector3 torque, float maxAngularAcceleration)
        {
            if (_body == null || !IsFiniteNonZero(torque))
                return;

            Vector3 clampedTorque = ClampTorqueByAngularAcceleration(torque, maxAngularAcceleration);
            if (!IsFiniteNonZero(clampedTorque))
                return;

            PhysicsForceRouter.QueueTorque(_body, clampedTorque, ForceMode.Force);
        }

        /// <summary>
        /// Applies an angular velocity change while clamping the equivalent angular acceleration.
        /// </summary>
        public void ApplyAngularVelocityChange(
            Vector3 angularVelocityChange,
            float maxAngularAcceleration,
            float fixedDeltaTime)
        {
            if (_body == null || !IsFiniteNonZero(angularVelocityChange))
                return;

            float safeFixedDeltaTime = math.max(fixedDeltaTime, 0.0001f);
            float allowedAngularVelocityDelta = math.max(0f, maxAngularAcceleration) * safeFixedDeltaTime;
            Vector3 clampedDelta = angularVelocityChange;
            float sqrMagnitude = clampedDelta.sqrMagnitude;
            if (allowedAngularVelocityDelta > 0f && sqrMagnitude > allowedAngularVelocityDelta * allowedAngularVelocityDelta)
                clampedDelta = SafeNormal(clampedDelta, Vector3.zero) * allowedAngularVelocityDelta;

            if (!IsFiniteNonZero(clampedDelta))
                return;

            PhysicsForceRouter.QueueTorque(_body, clampedDelta, ForceMode.VelocityChange);
        }

        /// <summary>
        /// Splits an off-center force into linear force plus capped torque around the center of mass.
        /// </summary>
        public void ApplyForceAtPositionSplit(
            Vector3 force,
            Vector3 applicationPoint,
            float maxLeverArm,
            float maxAngularAcceleration)
        {
            if (_body == null || !IsFiniteNonZero(force))
                return;

            Vector3 lever = applicationPoint - _body.worldCenterOfMass;
            float maxLeverArmSq = maxLeverArm * maxLeverArm;
            if (maxLeverArm > 0f && lever.sqrMagnitude > maxLeverArmSq)
                lever = SafeNormal(lever, Vector3.zero) * maxLeverArm;

            ApplyForce(force);

            if (lever.sqrMagnitude <= MinVectorMagnitudeSq)
                return;

            Vector3 torque = CrossVector(lever, force);
            ApplyTorque(torque, maxAngularAcceleration);
        }

        /// <summary>Writes linear velocity after finite validation.</summary>
        public void SetLinearVelocity(Vector3 velocity)
        {
            if (_body == null)
                return;

            _body.linearVelocity = SafeVelocity(velocity, _body.linearVelocity);
        }

        /// <summary>Projects the current linear velocity onto a collision plane.</summary>
        public void ProjectLinearVelocityOnPlane(Vector3 planeNormal)
        {
            if (_body == null || planeNormal.sqrMagnitude <= MinVectorMagnitudeSq)
                return;

            Vector3 projectedVelocity = ProjectVelocityOnCollisionPlane(_body.linearVelocity, planeNormal);
            SetLinearVelocity(projectedVelocity);
        }

        /// <summary>Moves the body kinematically after finite validation.</summary>
        public void MovePosition(Vector3 position)
        {
            if (_body == null)
                return;

            float3 position3 = new float3(position.x, position.y, position.z);
            if (!math.all(math.isfinite(position3)))
                return;

            _body.MovePosition(position);
        }

        /// <summary>
        /// Schedules a sweep-gated move probe. Resolution is deferred to the post-fixed swap window.
        /// </summary>
        public bool TrySweepGatedMove(
            Vector3 displacement,
            int layerMask,
            float skinWidth,
            out RaycastHit blockingHit)
        {
            if (_body == null || _capsule == null)
            {
                blockingHit = default;
                return false;
            }

            ResolveCapsulePoints(_body, _capsule, 0f, out Vector3 point1, out Vector3 point2, out float radius);
            return TrySweepGatedMove(
                displacement,
                layerMask,
                skinWidth,
                point1,
                point2,
                radius,
                ResolveSelfColliderInstanceId(),
                out blockingHit,
                out _);
        }

        /// <summary>
        /// Schedules a sweep-gated move using caller-supplied capsule metrics to avoid transform traversal in hot paths.
        /// </summary>
        public bool TrySweepGatedMove(
            Vector3 displacement,
            int layerMask,
            float skinWidth,
            Vector3 capsulePoint1,
            Vector3 capsulePoint2,
            float capsuleRadius,
            int selfColliderInstanceId,
            out RaycastHit blockingHit,
            out Vector3 resolvedPosition)
        {
            blockingHit = default;
            resolvedPosition = _body != null ? _body.position : Vector3.zero;
            if (_body == null)
                return false;

            if (!IsFiniteNonZero(displacement))
                return true;

            float3 displacement3 = new float3(displacement.x, displacement.y, displacement.z);
            float displacementSqr = math.lengthsq(displacement3);
            if (displacementSqr <= MinVectorMagnitudeSq)
                return true;

            float inverseDistance = math.rsqrt(displacementSqr);
            float distance = displacementSqr * inverseDistance;
            Vector3 direction = displacement * inverseDistance;
            ScheduleCapsuleSweepBatch(
                capsulePoint1,
                capsulePoint2,
                capsuleRadius,
                direction,
                distance,
                layerMask,
                skinWidth,
                selfColliderInstanceId);
            return false;
        }

        /// <summary>
        /// Applies the carrier-relative position formula using cached platform delta.
        /// </summary>
        public void ApplyCarrierMotion(
            Vector3 previousPlatformPosition,
            Vector3 currentPlatformPosition,
            Quaternion platformDeltaRotation,
            Vector3 localMoveWorld)
        {
            if (_body == null)
                return;

            Vector3 rotatedOffset = platformDeltaRotation * (_body.position - previousPlatformPosition);
            MovePosition(currentPlatformPosition + rotatedOffset + localMoveWorld);
        }

        /// <summary>
        /// Quadratic drag solve. Stable for variable fixed steps and cannot reverse velocity.
        /// </summary>
        public static Vector3 AnalyticalQuadraticDrag(Vector3 velocity, Vector3 dragCoefficient, float dt)
        {
            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            float3 drag3 = new float3(dragCoefficient.x, dragCoefficient.y, dragCoefficient.z);
            float speedMag = ApproximateSpeedMagnitude(velocity3);
            if (speedMag < DenormalVelocityFlushThresholdMetersPerSecond)
                return Vector3.zero;

            float safeDt = math.max(dt, 0.0001f);
            float3 denominator = 1f + math.max(drag3, 0f) * speedMag * safeDt;
            float3 result = velocity3 / math.max(denominator, new float3(0.001f));
            return SafeVelocity(new Vector3(result.x, result.y, result.z), velocity);
        }

        /// <summary>
        /// Scalar analytical quadratic drag solve: dv/dt = -k |v| v.
        /// Direction is preserved and reversal is impossible.
        /// </summary>
        public static Vector3 AnalyticalQuadraticDrag(Vector3 velocity, float dragCoefficient, float dt)
        {
            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            float speedSq = math.lengthsq(velocity3);
            if (speedSq < DenormalVelocityFlushThresholdMetersPerSecond * DenormalVelocityFlushThresholdMetersPerSecond)
                return Vector3.zero;

            float speed = ApproximateSpeedMagnitude(velocity3);
            float safeDt = math.max(dt, 0.0001f);
            float denominator = 1f + math.max(0f, dragCoefficient) * speed * safeDt;
            float3 result = velocity3 / math.max(denominator, 0.001f);
            return SafeVelocity(new Vector3(result.x, result.y, result.z), velocity);
        }

        /// <summary>
        /// Directional analytical drag with a cross-sectional forward area term.
        /// </summary>
        public static Vector3 AnalyticalQuadraticDrag(
            Vector3 velocity,
            float dragCoefficient,
            Vector3 forward,
            float crossSectionalAreaScale,
            float dt)
        {
            Vector3 safeVelocity = SafeVelocity(velocity);
            float3 velocity3 = new float3(safeVelocity.x, safeVelocity.y, safeVelocity.z);
            float speedSq = math.lengthsq(velocity3);
            if (speedSq < DenormalVelocityFlushThresholdMetersPerSecond * DenormalVelocityFlushThresholdMetersPerSecond)
                return Vector3.zero;

            float speed = ApproximateSpeedMagnitude(velocity3);
            float3 velocityDirection = velocity3 * math.rsqrt(speedSq);
            Vector3 safeForwardVector = SafeNormal(forward, Vector3.forward);
            float3 safeForward = new float3(safeForwardVector.x, safeForwardVector.y, safeForwardVector.z);
            float forwardExposure = math.max(0.2f, math.abs(math.dot(velocityDirection, safeForward)));
            float lateralExposure = math.max(0.2f, 1f - forwardExposure);
            float directionalCrossSection = math.max(forwardExposure, lateralExposure * 2.75f);
            float areaScale = math.max(0.01f, crossSectionalAreaScale) * directionalCrossSection;
            float safeDt = math.max(dt, 0.0001f);
            float denominator = 1f + math.max(0f, dragCoefficient) * areaScale * speed * safeDt;
            float3 result = velocity3 / math.max(denominator, 0.001f);
            return SafeVelocity(new Vector3(result.x, result.y, result.z), velocity);
        }

        /// <summary>Clears transient runtime state.</summary>
        public void ResetRuntimeState()
        {
            _isGrounded = false;
            ResetWallSlideContactState();
            ResetHydrodynamicAddedMassState();
            if (_scheduledSweepPending)
                DispatcherJobSwap.TryComplete(ref _nativeState.ScheduledSweepHandle, forceComplete: true);
            if (_kinematicRepairTargetProbePending)
                DispatcherJobSwap.TryComplete(ref _nativeState.KinematicRepairTargetHandle, forceComplete: true);

            _scheduledSweepPending = false;
            _scheduledSweepResultReady = false;
            _scheduledSweepWasBlocked = false;
            _scheduledSweepBlockingHit = default;
            _scheduledSweepResolvedPosition = _body != null ? _body.position : Vector3.zero;
            _scheduledSweepBlockedSpeed = 0f;
            _scheduledSweepShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _scheduledSweepBodyBindEpoch = _bodyBindEpoch;
            _lastBatchedFootstepHit = default;
            _lastBatchedFootstepPhysicsFrame = -1;
            _lastBatchedFootstepShiftSequence = _scheduledSweepShiftSequence;
            _lastBatchedFootstepBodyBindEpoch = _bodyBindEpoch;
            _kinematicRepairTargetProbePending = false;
            _kinematicRepairSnapReady = false;
            _kinematicRepairTargetBodyBindEpoch = _bodyBindEpoch;
            _kinematicRepairTargetShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _kinematicRepairSnapBodyBindEpoch = _bodyBindEpoch;
            _kinematicRepairSnapShiftSequence = _kinematicRepairTargetShiftSequence;
            _kinematicRepairTargetProbe = default;
            _kinematicRepairSnapPoint = default;
        }

        /// <summary>
        /// Returns current velocity. Added-mass presentation history is intentionally purged.
        /// </summary>
        public Vector3 ResolveHydrodynamicAddedMassVelocity(Vector3 actualVelocity, float submersionFactor)
        {
            return SafeVelocity(actualVelocity);
        }

        /// <summary>Legacy compatibility hook. Velocity history is purged, so this is intentionally a no-op.</summary>
        public void ResetHydrodynamicAddedMassState()
        {
        }

        private void ResetBodyBoundCachedResults()
        {
            ResetWallSlideContactState();
            _lastBatchedFootstepHit = default;
            _lastBatchedFootstepPhysicsFrame = -1;
            _lastBatchedFootstepShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _lastBatchedFootstepBodyBindEpoch = _bodyBindEpoch;
            _scheduledSweepResultReady = false;
            _scheduledSweepWasBlocked = false;
            _scheduledSweepBlockingHit = default;
            _scheduledSweepResolvedPosition = _body != null ? _body.position : Vector3.zero;
            _scheduledSweepBlockedSpeed = 0f;
            _kinematicRepairSnapReady = false;
            _kinematicRepairTargetProbe = default;
            _kinematicRepairSnapPoint = default;
            _kinematicRepairSnapShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _kinematicRepairSnapBodyBindEpoch = _bodyBindEpoch;
        }

        /// <summary>
        /// Schedules a jobified capsule sweep batch for the LateFrame locomotion integration window.
        /// This is the Burst/job seam used by high-speed locomotion probes. Consumption is intentionally deferred.
        /// </summary>
        public bool ScheduleCapsuleSweepBatch(
            Vector3 capsulePoint1,
            Vector3 capsulePoint2,
            float capsuleRadius,
            Vector3 direction,
            float distance,
            int layerMask,
            float skinWidth,
            int selfColliderInstanceId)
        {
            if (_scheduledSweepPending ||
                layerMask == 0 ||
                distance <= 0.0001f ||
                !math.isfinite(distance) ||
                !math.isfinite(skinWidth) ||
                !math.isfinite(capsuleRadius) ||
                !IsFiniteNonZero(direction) ||
                HectonFloatingOrigin.IsShiftInProgress)
            {
                return false;
            }

            float3 point13 = new float3(capsulePoint1.x, capsulePoint1.y, capsulePoint1.z);
            float3 point23 = new float3(capsulePoint2.x, capsulePoint2.y, capsulePoint2.z);
            if (!math.all(math.isfinite(point13)) || !math.all(math.isfinite(point23)))
                return false;

            float safeRadius = math.max(0.01f, capsuleRadius);
            float safeSkinWidth = math.max(0f, skinWidth);
            float safeDistance = math.max(0.0001f, distance);
            Vector3 safeDirection = SafeNormal(direction, Vector3.forward);

            EnsureScheduledSweepState();
            _scheduledSweepShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _scheduledSweepBodyBindEpoch = _bodyBindEpoch;
            _scheduledSweepState = new ScheduledSweepState
            {
                StartPosition = _body != null ? _body.position : Vector3.zero,
                CapsulePoint1 = capsulePoint1,
                CapsulePoint2 = capsulePoint2,
                CapsuleRadius = safeRadius,
                Direction = safeDirection,
                Distance = safeDistance,
                LayerMask = layerMask,
                SkinWidth = safeSkinWidth,
                SelfColliderInstanceId = selfColliderInstanceId
            };

            QueryParameters sweepQuery = new QueryParameters(layerMask, false, QueryTriggerInteraction.Ignore);
            _nativeState.ScheduledSweepCommands[ScheduledLocomotionSweepCommandIndex] = new CapsulecastCommand(
                capsulePoint1,
                capsulePoint2,
                safeRadius,
                safeDirection,
                sweepQuery,
                safeDistance + safeSkinWidth);

            _nativeState.ScheduledSweepCommands[ScheduledFootstepProbeCommandIndex] = new CapsulecastCommand(
                capsulePoint1,
                capsulePoint2,
                safeRadius,
                Vector3.down,
                sweepQuery,
                math.max(ScheduledFootstepProbeDistance, safeSkinWidth + 0.05f));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long scheduleBudgetStart = BeginSweepBudgetSample();
#endif
            using (_scheduledSweepProfilerMarker.Auto())
            {
                _nativeState.ScheduledSweepHandle = CapsulecastCommand.ScheduleBatch(
                    _nativeState.ScheduledSweepCommands,
                    _nativeState.ScheduledSweepResults,
                    1,
                    ScheduledSweepMaxHitsPerCommand,
                    default);
                _scheduledSweepPending = true;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EndSweepBudgetSample(scheduleBudgetStart, _sweepScheduleBudgetWarningHash);
#endif

            return true;
        }

        /// <summary>
        /// Schedules a forward interactable ray for physical hand IK repair targets. Results are consumed in LateFrame.
        /// </summary>
        public bool ScheduleKinematicRepairTargetProbe(
            Vector3 origin,
            Vector3 direction,
            float distance,
            int layerMask,
            float surfaceOffset)
        {
            if (_kinematicRepairTargetProbePending ||
                distance <= KinematicRepairTargetMinDistance ||
                !math.isfinite(distance) ||
                !math.isfinite(surfaceOffset) ||
                layerMask == 0 ||
                !IsFiniteNonZero(direction) ||
                HectonFloatingOrigin.IsShiftInProgress)
            {
                return false;
            }

            float3 origin3 = new float3(origin.x, origin.y, origin.z);
            if (!math.all(math.isfinite(origin3)))
                return false;

            EnsureKinematicRepairTargetState();
            _kinematicRepairTargetShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _kinematicRepairTargetBodyBindEpoch = _bodyBindEpoch;
            Vector3 safeDirection = SafeNormal(direction, Vector3.forward);
            float safeDistance = math.max(KinematicRepairTargetMinDistance, distance);
            float safeSurfaceOffset = math.max(0f, surfaceOffset);
            _kinematicRepairProbeSurfaceOffset = safeSurfaceOffset;
            _kinematicRepairProbeOriginAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            _nativeState.KinematicRepairTargetCommands[0] = new RaycastCommand
            {
                from = origin,
                direction = safeDirection,
                distance = safeDistance,
                queryParameters = new QueryParameters
                {
                    layerMask = layerMask,
                    hitTriggers = QueryTriggerInteraction.Ignore,
                    hitBackfaces = false,
                    hitMultipleFaces = false
                }
            };

            _nativeState.KinematicRepairTargetHandle = RaycastCommand.ScheduleBatch(
                _nativeState.KinematicRepairTargetCommands,
                _nativeState.KinematicRepairTargetResults,
                KinematicRepairTargetMinCommandsPerJob,
                default);
            _kinematicRepairTargetProbePending = true;
            return true;
        }

        /// <summary>
        /// Returns true when a previously scheduled capsule sweep completed and produced a blocking hit.
        /// Caller owns the decision about when a completed query may be consumed.
        /// </summary>
        public bool TryConsumeScheduledCapsuleSweep(
            out bool wasBlocked,
            out RaycastHit blockingHit,
            out Vector3 resolvedPosition,
            out float blockedSpeed)
        {
            wasBlocked = false;
            blockingHit = default;
            resolvedPosition = _body != null ? _body.position : Vector3.zero;
            blockedSpeed = 0f;
            if (!_scheduledSweepResultReady)
                return false;

            using (_scheduledSweepConsumeProfilerMarker.Auto())
            {
                _scheduledSweepResultReady = false;
                wasBlocked = _scheduledSweepWasBlocked;
                blockingHit = _scheduledSweepBlockingHit;
                resolvedPosition = _scheduledSweepResolvedPosition;
                blockedSpeed = _scheduledSweepBlockedSpeed;
                return true;
            }
        }

        /// <inheritdoc />
        public void PostFixedTick(float fixedDeltaTime)
        {
            TryEmitWakeSiltDecal(fixedDeltaTime);
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            CompleteScheduledSweepInLateFrameSwapWindow();
            CompleteKinematicRepairTargetProbeInLateFrameSwapWindow();
        }

        private static bool IsFiniteNonZero(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3)) && math.lengthsq(value3) > MinVectorMagnitudeSq;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static long BeginSweepBudgetSample()
        {
            return System.Diagnostics.Stopwatch.GetTimestamp();
        }

        private static void EndSweepBudgetSample(long startTimestamp, uint warningHash)
        {
            if (startTimestamp == 0L)
                return;

            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds <= SweepSolveTelemetryBudgetMilliseconds)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                warningHash,
                _sweepTelemetryContextHash,
                (float)elapsedMilliseconds);
        }
#endif

        private void RecordWallSlideContact(
            in RaycastHit hit,
            Vector3 normal,
            float slideAngleDegrees,
            float blockedSpeed,
            float velocityReduction01)
        {
            _lastWallSlideNormal = SafeNormal(normal, Vector3.up);
            _lastWallSlidePoint = SafeVelocity(hit.point, _body != null ? _body.position : Vector3.zero);
            _lastWallSlideBlockedSpeed = math.max(0f, blockedSpeed);
            _lastWallSlideAngleDegrees = math.max(0f, slideAngleDegrees);
            _lastWallSlideVelocityReduction01 = math.saturate(velocityReduction01);
            _lastWallSlidePhysicsFrame = PhysicsFrame.Current;
            _lastWallSlideShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _lastWallSlideBodyBindEpoch = _bodyBindEpoch;
            _lastWallSlideIsVoxel = IsVoxelWallHit(in hit);
        }

        private void ResetWallSlideContactState()
        {
            _lastWallSlideNormal = Vector3.zero;
            _lastWallSlidePoint = Vector3.zero;
            _lastWallSlideBlockedSpeed = 0f;
            _lastWallSlideAngleDegrees = 0f;
            _lastWallSlideVelocityReduction01 = 0f;
            _lastWallSlidePhysicsFrame = -1;
            _lastWallSlideShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _lastWallSlideBodyBindEpoch = _bodyBindEpoch;
            _lastWallSlideIsVoxel = false;
        }

        private static bool IsVoxelWallHit(in RaycastHit hit)
        {
            Collider hitCollider = hit.collider;
            if (hitCollider == null)
                return false;

            int hitLayer = hitCollider.gameObject.layer;
            return hitLayer == HectonLayerMasks.VoxelCave ||
                   hitLayer == HectonLayerMasks.VoxelProxy;
        }

        private static void ResolveProjectionTelemetry(
            Vector3 intendedDisplacement,
            Vector3 projectedDisplacement,
            out float slideAngleDegrees,
            out float velocityReduction01)
        {
            float intendedMagnitude = ApproximateSpeedMagnitude(new float3(
                intendedDisplacement.x,
                intendedDisplacement.y,
                intendedDisplacement.z));
            if (intendedMagnitude <= DenormalVelocityFlushThresholdMetersPerSecond)
            {
                slideAngleDegrees = 0f;
                velocityReduction01 = 0f;
                return;
            }

            float projectedMagnitude = ApproximateSpeedMagnitude(new float3(
                projectedDisplacement.x,
                projectedDisplacement.y,
                projectedDisplacement.z));
            if (projectedMagnitude <= DenormalVelocityFlushThresholdMetersPerSecond)
            {
                slideAngleDegrees = 90f;
                velocityReduction01 = 1f;
                return;
            }

            velocityReduction01 = math.saturate(1f - (projectedMagnitude / math.max(intendedMagnitude, 0.0001f)));
            slideAngleDegrees = velocityReduction01 * (135f - (45f * velocityReduction01));
        }

        private static int GetHitColliderInstanceId(in RaycastHit hit)
        {
            return unchecked((int)EntityId.ToULong(hit.colliderEntityId));
        }

        private void TryEmitWakeSiltDecal(float fixedDeltaTime)
        {
            if (_body == null)
                return;

            float safeDeltaTime = math.max(0f, fixedDeltaTime);
            _wakeSiltEmissionCooldown = math.max(0f, _wakeSiltEmissionCooldown - safeDeltaTime);
            if (_wakeSiltEmissionCooldown > 0f)
                return;

            Vector3 velocity = SafeVelocity(_body.linearVelocity);
            float speedSq = velocity.sqrMagnitude;
            float thresholdSq = WakeSiltEmissionSpeedThresholdMetersPerSecond * WakeSiltEmissionSpeedThresholdMetersPerSecond;
            if (speedSq <= thresholdSq)
                return;

            Vector3 emitPosition = _body.worldCenterOfMass;
            float3 emitPosition3 = new float3(emitPosition.x, emitPosition.y, emitPosition.z);
            if (!math.all(math.isfinite(emitPosition3)))
                return;

            AbyssalFluidDecalManager fluidDecals = GlobalRegistry.AbyssalFluidDecals;
            if (fluidDecals == null)
                return;

            AbsoluteUniversePosition emitAup = AbsoluteUniversePosition.FromRuntimePosition(emitPosition);
            float3 runtimeFromAup = emitAup.ToRuntimeFloat3();
            Vector3 aupRuntimePosition = new Vector3(runtimeFromAup.x, runtimeFromAup.y, runtimeFromAup.z);
            float approximateSpeed = ApproximateSpeedMagnitude(new float3(velocity.x, velocity.y, velocity.z));
            float intensity01 = math.saturate((approximateSpeed - WakeSiltEmissionSpeedThresholdMetersPerSecond) / 8f);
            fluidDecals.RegisterWakeSilt(aupRuntimePosition, velocity, intensity01);
            _wakeSiltEmissionCooldown = WakeSiltEmissionCooldownSeconds;
        }

        internal static Vector3 SafeVelocity(Vector3 velocity, Vector3 fallback = default)
        {
            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            return math.all(math.isfinite(velocity3)) ? velocity : fallback;
        }

        private static float ApproximateSpeedMagnitude(float3 velocity)
        {
            float3 absolute = math.abs(velocity);
            float maxComponent = math.cmax(absolute);
            float minComponent = math.cmin(absolute);
            float midComponent = absolute.x + absolute.y + absolute.z - maxComponent - minComponent;
            return maxComponent + (midComponent * 0.375f) + (minComponent * 0.125f);
        }

        public static Vector3 SafeNormal(Vector3 value, Vector3 fallback)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            if (!math.all(math.isfinite(value3)))
                return fallback;

            float sqrMagnitude = math.lengthsq(value3);
            if (sqrMagnitude <= MinVectorMagnitudeSq)
                return fallback;

            float inverseMagnitude = math.rsqrt(sqrMagnitude);
            Vector3 normalized = value * inverseMagnitude;
            return SafeVelocity(normalized, fallback);
        }

        private static Vector3 CrossVector(Vector3 a, Vector3 b)
        {
            return new Vector3(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x);
        }

        private static Quaternion ResolveLookRotationNoTrig(Vector3 forward, Vector3 up)
        {
            Vector3 f = SafeNormal(forward, Vector3.forward);
            Vector3 u = SafeNormal(up, Vector3.up);
            if (math.abs(math.dot((float3)f, (float3)u)) > 0.94f)
                u = math.abs(f.y) < 0.94f ? Vector3.up : Vector3.right;

            Vector3 r = SafeNormal(CrossVector(u, f), Vector3.right);
            u = SafeNormal(CrossVector(f, r), Vector3.up);

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

            float lengthSq = math.max(math.dot(q, q), 0.000001f);
            q *= math.rsqrt(lengthSq);
            return new Quaternion(q.x, q.y, q.z, q.w);
        }

        internal static Vector3 ProjectVelocityOnCollisionPlane(Vector3 velocity, Vector3 hitNormal)
        {
            Vector3 safeVelocity = SafeVelocity(velocity);
            Vector3 safeNormal = SafeNormal(hitNormal, Vector3.up);
            float normalVelocity = math.dot((float3)safeVelocity, (float3)safeNormal);
            Vector3 projectedVelocity = safeVelocity - (safeNormal * normalVelocity);
            return SafeVelocity(projectedVelocity, Vector3.zero);
        }

        private static Vector3 ProjectVelocityOnUnitCollisionPlane(Vector3 velocity, Vector3 unitNormal)
        {
            Vector3 safeVelocity = SafeVelocity(velocity);
            float normalVelocity = math.dot((float3)safeVelocity, (float3)unitNormal);
            Vector3 projectedVelocity = safeVelocity - (unitNormal * normalVelocity);
            return SafeVelocity(projectedVelocity, Vector3.zero);
        }

        private static Vector3 ResolveVoxelProxySlideVelocity(
            Vector3 velocity,
            Vector3 unitNormal,
            Vector3 tangentSlideDisplacement)
        {
            Vector3 projectedVelocity = ProjectVelocityOnUnitCollisionPlane(velocity, unitNormal) * VoxelProxySlideVelocityRetain;
            if (projectedVelocity.sqrMagnitude > MinVectorMagnitudeSq)
                return SafeVelocity(projectedVelocity, velocity);

            float tangentSlideSqr = tangentSlideDisplacement.sqrMagnitude;
            if (tangentSlideSqr <= MinVectorMagnitudeSq)
                return Vector3.zero;

            return SafeVelocity(tangentSlideDisplacement * (VoxelProxyGlideFallbackMetersPerSecond * math.rsqrt(tangentSlideSqr)));
        }

        internal static float ResolveHeavyBrineSinkMultiplier(float fluidDensityKgPerCubicMeter, float referenceSeaWaterDensityKgPerCubicMeter)
        {
            if (!math.isfinite(fluidDensityKgPerCubicMeter) ||
                !math.isfinite(referenceSeaWaterDensityKgPerCubicMeter) ||
                fluidDensityKgPerCubicMeter <= referenceSeaWaterDensityKgPerCubicMeter)
            {
                return 0f;
            }

            float densityExcess01 = math.saturate(
                (fluidDensityKgPerCubicMeter - referenceSeaWaterDensityKgPerCubicMeter) /
                math.max(1f, referenceSeaWaterDensityKgPerCubicMeter * 0.25f));
            return -math.lerp(0.35f, 0.85f, densityExcess01);
        }

        internal static Vector3 ResolveBuoyancyInversionVelocity(
            Vector3 velocity,
            bool insideHeavyBrine,
            bool thrusterActive,
            float sinkMultiplier)
        {
            Vector3 safeVelocity = SafeVelocity(velocity);
            if (!insideHeavyBrine ||
                thrusterActive ||
                safeVelocity.y >= 0f ||
                sinkMultiplier >= 0f)
            {
                return safeVelocity;
            }

            safeVelocity.y *= sinkMultiplier;
            return SafeVelocity(safeVelocity, Vector3.zero);
        }

        private void EnsureScheduledSweepState()
        {
            _nativeState.EnsureScheduledSweepState(ScheduledSweepCommandCount, ScheduledSweepMaxHits);
        }

        private void EnsureKinematicRepairTargetState()
        {
            _nativeState.EnsureKinematicRepairTargetState(
                KinematicRepairTargetCommandCount,
                KinematicRepairTargetResultCount);
        }

        private void DisposeScheduledSweepState()
        {
            _nativeState.DisposeScheduledSweepState(_scheduledSweepPending, _nativeState.ScheduledSweepHandle);
            _nativeState.DisposeKinematicRepairTargetState(
                _kinematicRepairTargetProbePending,
                _nativeState.KinematicRepairTargetHandle);

            _scheduledSweepPending = false;
            _scheduledSweepResultReady = false;
            _scheduledSweepWasBlocked = false;
            _scheduledSweepBlockingHit = default;
            _scheduledSweepResolvedPosition = _body != null ? _body.position : Vector3.zero;
            _scheduledSweepBlockedSpeed = 0f;
            _lastBatchedFootstepHit = default;
            _lastBatchedFootstepPhysicsFrame = -1;
            _kinematicRepairTargetProbePending = false;
            _kinematicRepairSnapReady = false;
            _kinematicRepairTargetProbe = default;
            _kinematicRepairSnapPoint = default;
        }

        private void TryRegisterPostFixedTick()
        {
            if (_registeredPostFixedTick || !Application.isPlaying)
                return;

            _registeredPostFixedTick = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterPostFixedTick()
        {
            if (!_registeredPostFixedTick)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Player);
            _registeredPostFixedTick = false;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_registeredLateFrameTick || !Application.isPlaying)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrameTick = false;
        }

        private void TryRegisterMotorService()
        {
            if (_registeredMotorService || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterPlayerMotorService(this);
            _registeredMotorService = ReferenceEquals(GlobalRegistry.PlayerMotor, this);
        }

        private void TryUnregisterMotorService()
        {
            if (!_registeredMotorService)
                return;

            GlobalRegistry.UnregisterPlayerMotorService(this);
            _registeredMotorService = false;
        }

        private void CompleteScheduledSweepInLateFrameSwapWindow()
        {
            if (!_scheduledSweepPending)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _nativeState.ScheduledSweepHandle, forceComplete: false))
                return;

            if (HectonFloatingOrigin.IsShiftInProgress ||
                _scheduledSweepShiftSequence != HectonFloatingOrigin.CurrentShiftSequence ||
                _scheduledSweepBodyBindEpoch != _bodyBindEpoch)
            {
                DiscardScheduledSweepResults();
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long consumeBudgetStart = BeginSweepBudgetSample();
#endif
            _scheduledSweepPending = false;
            _scheduledSweepResultReady = true;
            _scheduledSweepWasBlocked = false;
            _scheduledSweepBlockingHit = default;
            _scheduledSweepBlockedSpeed = 0f;
            _scheduledSweepResolvedPosition = _body != null ? _body.position : _scheduledSweepState.StartPosition;

            ConsumeScheduledFootstepProbe();

            float nearestDistance = float.MaxValue;
            int nearestIndex = -1;
            bool nearestHitIsVoxelProxy = false;
            for (int i = 0; i < ScheduledSweepMaxHitsPerCommand; i++)
            {
                RaycastHit hit = _nativeState.ScheduledSweepResults[i];
                int hitColliderInstanceId = GetHitColliderInstanceId(in hit);
                if (hitColliderInstanceId == 0 || hitColliderInstanceId == _scheduledSweepState.SelfColliderInstanceId)
                    continue;

                if (!math.isfinite(hit.distance) || hit.distance < 0f)
                    continue;

                Collider hitCollider = hit.collider;
                bool isVoxelProxyHit = hitCollider != null && hitCollider.gameObject.layer == HectonLayerMasks.VoxelProxy;

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestIndex = i;
                    nearestHitIsVoxelProxy = isVoxelProxyHit;
                }
            }

            if (nearestIndex < 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                EndSweepBudgetSample(consumeBudgetStart, _sweepConsumeBudgetWarningHash);
#endif
                return;
            }

            _scheduledSweepWasBlocked = true;
            _scheduledSweepBlockingHit = _nativeState.ScheduledSweepResults[nearestIndex];
            Vector3 safeNormal = SafeNormal(_scheduledSweepBlockingHit.normal, Vector3.up);
            float safeDistance = math.max(0f, _scheduledSweepBlockingHit.distance - _scheduledSweepState.SkinWidth);
            float penetrationDepth = math.max(0f, _scheduledSweepState.SkinWidth - _scheduledSweepBlockingHit.distance);
            Vector3 intendedDisplacement = _scheduledSweepState.Direction * _scheduledSweepState.Distance;
            float displacementIntoWall = math.dot((float3)intendedDisplacement, (float3)safeNormal);
            Vector3 projectedDisplacement = intendedDisplacement - (safeNormal * displacementIntoWall);
            Vector3 resolvedDisplacement = _scheduledSweepState.Direction * safeDistance;
            Vector3 proxyTangentSlideDisplacement = Vector3.zero;
            if (nearestHitIsVoxelProxy)
            {
                float remainingDistance = math.max(0f, _scheduledSweepState.Distance - safeDistance);
                float projectedDisplacementSqr = projectedDisplacement.sqrMagnitude;
                if (math.isfinite(projectedDisplacementSqr) &&
                    projectedDisplacementSqr > MinVectorMagnitudeSq &&
                    remainingDistance > 0f)
                {
                    proxyTangentSlideDisplacement = projectedDisplacement *
                        (remainingDistance * VoxelProxySlideDistanceRetain * math.rsqrt(projectedDisplacementSqr));
                    resolvedDisplacement += proxyTangentSlideDisplacement;
                }

                _scheduledSweepResolvedPosition = _scheduledSweepState.StartPosition + resolvedDisplacement;
            }
            else
            {
                _scheduledSweepResolvedPosition = _scheduledSweepState.StartPosition +
                    resolvedDisplacement +
                    (safeNormal * penetrationDepth);
            }

            Vector3 previousVelocity = _body != null ? _body.linearVelocity : Vector3.zero;
            Vector3 projectedVelocity = ProjectVelocityOnUnitCollisionPlane(previousVelocity, safeNormal);
            Vector3 rejectedVelocity = previousVelocity - projectedVelocity;
            _scheduledSweepBlockedSpeed = ApproximateSpeedMagnitude(new float3(
                rejectedVelocity.x,
                rejectedVelocity.y,
                rejectedVelocity.z));
            if (nearestHitIsVoxelProxy)
            {
                projectedVelocity = ResolveVoxelProxySlideVelocity(previousVelocity, safeNormal, proxyTangentSlideDisplacement);
                _scheduledSweepBlockedSpeed *= 1f - VoxelProxySlideVelocityRetain;
            }

            if (safeNormal.y < WallSlideTelemetryMaxNormalY)
            {
                ResolveProjectionTelemetry(
                    intendedDisplacement,
                    projectedDisplacement,
                    out float slideAngleDegrees,
                    out float velocityReduction01);
                RecordWallSlideContact(
                    in _scheduledSweepBlockingHit,
                    safeNormal,
                    slideAngleDegrees,
                    _scheduledSweepBlockedSpeed,
                    velocityReduction01);
            }

            MovePosition(_scheduledSweepResolvedPosition);
            SetLinearVelocity(projectedVelocity);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EndSweepBudgetSample(consumeBudgetStart, _sweepConsumeBudgetWarningHash);
#endif
        }

        private void ConsumeScheduledFootstepProbe()
        {
            _lastBatchedFootstepHit = default;
            _lastBatchedFootstepPhysicsFrame = -1;
            _lastBatchedFootstepShiftSequence = _scheduledSweepShiftSequence;
            _lastBatchedFootstepBodyBindEpoch = _scheduledSweepBodyBindEpoch;

            float nearestDistance = float.MaxValue;
            int nearestIndex = -1;
            int startIndex = ScheduledFootstepProbeCommandIndex * ScheduledSweepMaxHitsPerCommand;
            int endIndex = startIndex + ScheduledSweepMaxHitsPerCommand;
            for (int i = startIndex; i < endIndex; i++)
            {
                RaycastHit hit = _nativeState.ScheduledSweepResults[i];
                int hitColliderInstanceId = GetHitColliderInstanceId(in hit);
                if (hitColliderInstanceId == 0 || hitColliderInstanceId == _scheduledSweepState.SelfColliderInstanceId)
                    continue;

                if (!math.isfinite(hit.distance) || hit.distance < 0f)
                    continue;

                if (!IsValidSupportFootstepHit(in hit))
                    continue;

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestIndex = i;
                }
            }

            if (nearestIndex < 0)
                return;

            _lastBatchedFootstepHit = _nativeState.ScheduledSweepResults[nearestIndex];
            _lastBatchedFootstepPhysicsFrame = PhysicsFrame.Current;
            _lastBatchedFootstepShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _lastBatchedFootstepBodyBindEpoch = _bodyBindEpoch;
        }

        private void DiscardScheduledSweepResults()
        {
            _scheduledSweepPending = false;
            _scheduledSweepResultReady = false;
            _scheduledSweepWasBlocked = false;
            _scheduledSweepBlockingHit = default;
            _scheduledSweepResolvedPosition = _body != null ? _body.position : _scheduledSweepState.StartPosition;
            _scheduledSweepBlockedSpeed = 0f;
            _lastBatchedFootstepHit = default;
            _lastBatchedFootstepPhysicsFrame = -1;
            _lastBatchedFootstepShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _lastBatchedFootstepBodyBindEpoch = _bodyBindEpoch;
            _nativeState.ScheduledSweepHandle = default;
        }

        private static bool IsValidSupportFootstepHit(in RaycastHit hit)
        {
            if (!math.isfinite(hit.distance) || hit.distance < 0f)
                return false;

            Vector3 normal = hit.normal;
            if (!math.isfinite(normal.x) || !math.isfinite(normal.y) || !math.isfinite(normal.z))
                return false;

            float normalSq = normal.sqrMagnitude;
            if (!math.isfinite(normalSq) || normalSq <= MinVectorMagnitudeSq || normal.y <= 0f)
                return false;

            return (normal.y * normal.y) >= ScheduledFootstepProbeMinSupportNormalYSq * normalSq;
        }

        private void CompleteKinematicRepairTargetProbeInLateFrameSwapWindow()
        {
            if (!_kinematicRepairTargetProbePending)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _nativeState.KinematicRepairTargetHandle, forceComplete: false))
                return;

            if (HectonFloatingOrigin.IsShiftInProgress ||
                _kinematicRepairTargetShiftSequence != HectonFloatingOrigin.CurrentShiftSequence ||
                _kinematicRepairTargetBodyBindEpoch != _bodyBindEpoch)
            {
                DiscardKinematicRepairTargetProbe();
                return;
            }

            _kinematicRepairTargetProbePending = false;
            _kinematicRepairSnapReady = false;
            _kinematicRepairTargetProbe = default;
            _kinematicRepairSnapPoint = default;

            RaycastHit hit = _nativeState.KinematicRepairTargetResults[0];
            int colliderInstanceId = GetHitColliderInstanceId(in hit);
            if (colliderInstanceId == 0 ||
                !math.isfinite(hit.distance) ||
                hit.distance <= KinematicRepairTargetMinDistance)
                return;

            RaycastCommand command = _nativeState.KinematicRepairTargetCommands[0];
            if (!math.isfinite(command.distance) || hit.distance > command.distance)
                return;

            Vector3 rayDirection = SafeNormal(command.direction, Vector3.forward);
            float3 hitNormal3 = new float3(hit.normal.x, hit.normal.y, hit.normal.z);
            if (!math.all(math.isfinite(hitNormal3)) || math.dot(hitNormal3, (float3)rayDirection) >= 0f)
                return;

            Vector3 safeNormal = SafeNormal(hit.normal, -rayDirection);
            Vector3 fallbackPoint = command.from + (rayDirection * hit.distance);
            Vector3 hitPoint = SafeVelocity(hit.point, fallbackPoint);
            Vector3 snapRuntimePosition = hitPoint + (safeNormal * _kinematicRepairProbeSurfaceOffset);
            float3 snapRuntimePosition3 = new float3(snapRuntimePosition.x, snapRuntimePosition.y, snapRuntimePosition.z);
            if (!math.all(math.isfinite(snapRuntimePosition3)))
                return;

            AbsoluteUniversePosition snapAup = AbsoluteUniversePosition.FromRuntimePosition(snapRuntimePosition);
            AbsoluteUniversePosition hitAup = AbsoluteUniversePosition.FromRuntimePosition(hitPoint);
            Vector3 toolUp = math.abs(math.dot((float3)safeNormal, new float3(0f, 1f, 0f))) > 0.95f ? Vector3.forward : Vector3.up;
            _kinematicRepairTargetProbe = new KinematicRepairTargetProbe(
                _kinematicRepairProbeOriginAup,
                hitAup,
                rayDirection,
                safeNormal,
                hit.distance,
                colliderInstanceId);

            _kinematicRepairSnapPoint = new KinematicRepairSnapPoint
            {
                AnchorAup = snapAup,
                LeftHandAup = snapAup,
                RightHandAup = snapAup,
                RuntimePosition = snapRuntimePosition,
                SurfaceNormal = safeNormal,
                ToolRotation = ResolveLookRotationNoTrig(-safeNormal, toolUp),
                HitDistance = hit.distance,
                Blend = 1f,
                ColliderInstanceId = colliderInstanceId
            };
            _kinematicRepairSnapReady = true;
            _kinematicRepairSnapShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _kinematicRepairSnapBodyBindEpoch = _bodyBindEpoch;
        }

        private void DiscardKinematicRepairTargetProbe()
        {
            _kinematicRepairTargetProbePending = false;
            _kinematicRepairSnapReady = false;
            _kinematicRepairTargetProbe = default;
            _kinematicRepairSnapPoint = default;
            _kinematicRepairSnapShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _kinematicRepairSnapBodyBindEpoch = _bodyBindEpoch;
            _nativeState.KinematicRepairTargetHandle = default;
        }

        private int ResolveSelfColliderInstanceId()
        {
            return _capsule != null ? unchecked((int)EntityId.ToULong(_capsule.GetEntityId())) : 0;
        }

        private Vector3 ClampTorqueByAngularAcceleration(Vector3 worldTorque, float maxAngularAcceleration)
        {
            if (_body == null || maxAngularAcceleration <= 0f)
                return worldTorque;

            Quaternion inertiaRotation = _body.rotation * _body.inertiaTensorRotation;
            Quaternion worldToInertia = Quaternion.Inverse(inertiaRotation);
            Vector3 localTorque = worldToInertia * worldTorque;
            Vector3 inertiaTensor = _body.inertiaTensor;
            localTorque.x = ClampTorqueAxis(localTorque.x, inertiaTensor.x, maxAngularAcceleration);
            localTorque.y = ClampTorqueAxis(localTorque.y, inertiaTensor.y, maxAngularAcceleration);
            localTorque.z = ClampTorqueAxis(localTorque.z, inertiaTensor.z, maxAngularAcceleration);
            return inertiaRotation * localTorque;
        }

        private static float ClampTorqueAxis(float torque, float inertiaAxis, float maxAngularAcceleration)
        {
            float safeInertia = math.max(0.0001f, inertiaAxis);
            float angularAcceleration = torque / safeInertia;
            if (!math.isfinite(angularAcceleration))
                return 0f;

            angularAcceleration = math.clamp(angularAcceleration, -maxAngularAcceleration, maxAngularAcceleration);
            return angularAcceleration * safeInertia;
        }

        private static void ResolveCapsulePoints(
            Rigidbody body,
            CapsuleCollider capsule,
            float inset,
            out Vector3 point1,
            out Vector3 point2,
            out float radius)
        {
            Transform bodyTransform = body.transform;
            Vector3 lossyScale = bodyTransform.lossyScale;
            float radiusScale = math.max(math.abs(lossyScale.x), math.abs(lossyScale.z));
            float heightScale = math.abs(lossyScale.y);
            radius = math.max(0.01f, capsule.radius * radiusScale - inset);
            float halfHeight = math.max(radius, (capsule.height * heightScale) * 0.5f);
            Vector3 center = bodyTransform.TransformPoint(capsule.center);
            Vector3 up = bodyTransform.up;
            float segmentHalf = math.max(0f, halfHeight - radius - inset);
            point1 = center + up * segmentHalf;
            point2 = center - up * segmentHalf;
        }
    }
}
