using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Physics;
using Hecton8.World;
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
        private const int ScheduledSweepCommandCount = 1;
        private const int ScheduledSweepMaxHits = 8;
        private const float DenormalVelocityFlushThresholdMetersPerSecond = 0.001f;
        private const float InventoryLoadMinimumMovementMultiplier = 0.62f;
        private const float WakeSiltEmissionSpeedThresholdMetersPerSecond = 4.5f;
        private const float WakeSiltEmissionCooldownSeconds = 0.35f;
        private const float WallSlideTelemetryMaxNormalY = 0.75f;
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
        private readonly RaycastHit[] _sweepHitBuffer = new RaycastHit[32]; // COLD ALLOC: RaycastHit[32] — motor-owned sweep/collision query buffer for kinematic isolation — owner: HectonPlayerMotor
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
        private bool _lastWallSlideIsVoxel;

        /// <inheritdoc />
        public Rigidbody Body => _body;

        /// <inheritdoc />
        public CapsuleCollider Capsule => _capsule;

        /// <inheritdoc />
        public bool IsGrounded => _isGrounded;

        /// <summary>Current event-driven carry-load movement scalar.</summary>
        public float EncumbranceMovementMultiplier => _encumbranceMovementMultiplier;

        /// <summary>Motor-owned sweep buffer. Reserved for kinematic queries only.</summary>
        public RaycastHit[] SweepHitBuffer => _sweepHitBuffer;

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

        /// <summary>Binds authoritative body references owned by the locomotion controller.</summary>
        public void Bind(Rigidbody body, CapsuleCollider capsule)
        {
            _body = body;
            _capsule = capsule;
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
                clampedDelta = clampedDelta.normalized * allowedAngularVelocityDelta;

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
                lever = lever.normalized * maxLeverArm;

            ApplyForce(force);

            if (lever.sqrMagnitude <= MinVectorMagnitudeSq)
                return;

            Vector3 torque = Vector3.Cross(lever, force);
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

            float distance = displacement.magnitude;
            if (distance <= 0.0001f)
                return true;

            Vector3 direction = displacement / distance;
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
        /// Analytical quadratic drag solve. Stable for variable fixed steps and cannot reverse velocity.
        /// </summary>
        public static Vector3 AnalyticalQuadraticDrag(Vector3 velocity, Vector3 dragCoefficient, float dt)
        {
            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            float3 drag3 = new float3(dragCoefficient.x, dragCoefficient.y, dragCoefficient.z);
            float speedMag = math.length(velocity3);
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
            float speed = velocity.magnitude;
            if (speed < DenormalVelocityFlushThresholdMetersPerSecond)
                return Vector3.zero;

            float safeDt = math.max(dt, 0.0001f);
            float denominator = 1f + math.max(0f, dragCoefficient) * speed * safeDt;
            return SafeVelocity(velocity / math.max(denominator, 0.001f), velocity);
        }

        /// <summary>Clears transient runtime state.</summary>
        public void ResetRuntimeState()
        {
            _isGrounded = false;
            ResetWallSlideContactState();
            ResetHydrodynamicAddedMassState();
            if (_scheduledSweepPending)
                DispatcherJobSwap.TryComplete(ref _nativeState.ScheduledSweepHandle, forceComplete: true);

            _scheduledSweepPending = false;
            _scheduledSweepResultReady = false;
            _scheduledSweepWasBlocked = false;
            _scheduledSweepBlockingHit = default;
            _scheduledSweepResolvedPosition = _body != null ? _body.position : Vector3.zero;
            _scheduledSweepBlockedSpeed = 0f;
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

        /// <summary>
        /// Schedules an asynchronous capsule sweep batch for a future locomotion integration window.
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
            if (_scheduledSweepPending || distance <= 0.0001f)
                return false;

            EnsureScheduledSweepState();
            _scheduledSweepState = new ScheduledSweepState
            {
                StartPosition = _body != null ? _body.position : Vector3.zero,
                CapsulePoint1 = capsulePoint1,
                CapsulePoint2 = capsulePoint2,
                CapsuleRadius = math.max(0.01f, capsuleRadius),
                Direction = direction,
                Distance = distance,
                LayerMask = layerMask,
                SkinWidth = skinWidth,
                SelfColliderInstanceId = selfColliderInstanceId
            };

            _nativeState.ScheduledSweepCommands[0] = new CapsulecastCommand(
                capsulePoint1,
                capsulePoint2,
                math.max(0.01f, capsuleRadius),
                direction,
                new QueryParameters(layerMask, false, QueryTriggerInteraction.Ignore),
                distance + skinWidth);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long scheduleBudgetStart = BeginSweepBudgetSample();
#endif
            using (_scheduledSweepProfilerMarker.Auto())
            {
                _nativeState.ScheduledSweepHandle = CapsulecastCommand.ScheduleBatch(
                    _nativeState.ScheduledSweepCommands,
                    _nativeState.ScheduledSweepResults,
                    1,
                    ScheduledSweepMaxHits,
                    default);
                _scheduledSweepPending = true;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EndSweepBudgetSample(scheduleBudgetStart, _sweepScheduleBudgetWarningHash);
#endif

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
            _lastWallSlideIsVoxel = false;
        }

        private static bool IsVoxelWallHit(in RaycastHit hit)
        {
            Collider hitCollider = hit.collider;
            return hitCollider != null && hitCollider.gameObject.layer == HectonLayerMasks.VoxelCave;
        }

        private float ResolveSlideBlockedSpeed(Vector3 intendedDisplacement, Vector3 slideDisplacement, Vector3 wallNormal)
        {
            Vector3 previousVelocity = _body != null ? SafeVelocity(_body.linearVelocity) : Vector3.zero;
            Vector3 projectedVelocity = ProjectVelocityOnCollisionPlane(previousVelocity, wallNormal);
            float velocityBlockedSpeed = (previousVelocity - projectedVelocity).magnitude;
            float rejectedSpeed = (intendedDisplacement - slideDisplacement).magnitude / math.max(Time.fixedDeltaTime, 0.0001f);
            return math.max(velocityBlockedSpeed, rejectedSpeed);
        }

        private static float ResolveProjectionAngleDegrees(Vector3 intendedDisplacement, Vector3 projectedDisplacement)
        {
            float intendedSqr = intendedDisplacement.sqrMagnitude;
            if (intendedSqr <= MinVectorMagnitudeSq)
                return 0f;

            float projectedSqr = projectedDisplacement.sqrMagnitude;
            if (projectedSqr <= MinVectorMagnitudeSq)
                return 90f;

            float invMagnitude = 1f / math.sqrt(intendedSqr * projectedSqr);
            float dot = math.clamp(Vector3.Dot(intendedDisplacement, projectedDisplacement) * invMagnitude, -1f, 1f);
            return math.degrees(math.acos(dot));
        }

        private static float ResolveProjectionVelocityReduction01(Vector3 intendedDisplacement, Vector3 projectedDisplacement)
        {
            float intendedSqr = intendedDisplacement.sqrMagnitude;
            if (intendedSqr <= MinVectorMagnitudeSq)
                return 0f;

            float projectedSqr = projectedDisplacement.sqrMagnitude;
            if (projectedSqr <= MinVectorMagnitudeSq)
                return 1f;

            float intendedMagnitude = math.sqrt(intendedSqr);
            float projectedMagnitude = math.sqrt(projectedSqr);
            return math.saturate(1f - (projectedMagnitude / math.max(intendedMagnitude, 0.0001f)));
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
            float intensity01 = math.saturate((math.sqrt(speedSq) - WakeSiltEmissionSpeedThresholdMetersPerSecond) / 8f);
            fluidDecals.RegisterWakeSilt(aupRuntimePosition, velocity, intensity01);
            _wakeSiltEmissionCooldown = WakeSiltEmissionCooldownSeconds;
        }

        private bool TryFindNearestBlockingHit(
            Vector3 capsulePoint1,
            Vector3 capsulePoint2,
            float capsuleRadius,
            Vector3 direction,
            float distance,
            int layerMask,
            int selfColliderInstanceId,
            out RaycastHit nearestHit)
        {
            nearestHit = default;
            return false;
        }

        internal static Vector3 SafeVelocity(Vector3 velocity, Vector3 fallback = default)
        {
            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            return math.all(math.isfinite(velocity3)) ? velocity : fallback;
        }

        private static Vector3 SafeNormal(Vector3 value, Vector3 fallback)
        {
            float sqrMagnitude = value.sqrMagnitude;
            if (sqrMagnitude <= MinVectorMagnitudeSq)
                return fallback;

            float inverseMagnitude = 1f / math.sqrt(sqrMagnitude);
            Vector3 normalized = value * inverseMagnitude;
            return SafeVelocity(normalized, fallback);
        }

        internal static Vector3 ProjectVelocityOnCollisionPlane(Vector3 velocity, Vector3 hitNormal)
        {
            Vector3 safeVelocity = SafeVelocity(velocity);
            Vector3 safeNormal = SafeNormal(hitNormal, Vector3.up);
            float normalVelocity = Vector3.Dot(safeVelocity, safeNormal);
            Vector3 projectedVelocity = safeVelocity - (safeNormal * normalVelocity);
            return SafeVelocity(projectedVelocity, Vector3.zero);
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

        private void DisposeScheduledSweepState()
        {
            _nativeState.DisposeScheduledSweepState(_scheduledSweepPending, _nativeState.ScheduledSweepHandle);

            _scheduledSweepPending = false;
            _scheduledSweepResultReady = false;
            _scheduledSweepWasBlocked = false;
            _scheduledSweepBlockingHit = default;
            _scheduledSweepResolvedPosition = _body != null ? _body.position : Vector3.zero;
            _scheduledSweepBlockedSpeed = 0f;
        }

        private void TryRegisterPostFixedTick()
        {
            if (_registeredPostFixedTick || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Player);
            _registeredPostFixedTick = SystemDispatcher.GetPostFixedLane(PriorityLayer.Player).Contains(this);
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

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrameTick = SystemDispatcher.GetLateFrameLane(PriorityLayer.Player).Contains(this);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long consumeBudgetStart = BeginSweepBudgetSample();
#endif
            _scheduledSweepPending = false;
            _scheduledSweepResultReady = true;
            _scheduledSweepWasBlocked = false;
            _scheduledSweepBlockingHit = default;
            _scheduledSweepBlockedSpeed = 0f;
            _scheduledSweepResolvedPosition = _body != null ? _body.position : _scheduledSweepState.StartPosition;

            float nearestDistance = float.MaxValue;
            int nearestIndex = -1;
            for (int i = 0; i < ScheduledSweepMaxHits; i++)
            {
                RaycastHit hit = _nativeState.ScheduledSweepResults[i];
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                EndSweepBudgetSample(consumeBudgetStart, _sweepConsumeBudgetWarningHash);
#endif
                return;
            }

            _scheduledSweepWasBlocked = true;
            _scheduledSweepBlockingHit = _nativeState.ScheduledSweepResults[nearestIndex];
            float safeDistance = math.max(0f, _scheduledSweepBlockingHit.distance - _scheduledSweepState.SkinWidth);
            float penetrationDepth = math.max(0f, _scheduledSweepState.SkinWidth - _scheduledSweepBlockingHit.distance);
            _scheduledSweepResolvedPosition = _scheduledSweepState.StartPosition +
                (_scheduledSweepState.Direction * safeDistance) +
                (_scheduledSweepBlockingHit.normal * penetrationDepth);

            Vector3 previousVelocity = _body != null ? _body.linearVelocity : Vector3.zero;
            Vector3 projectedVelocity = ProjectVelocityOnCollisionPlane(previousVelocity, _scheduledSweepBlockingHit.normal);
            _scheduledSweepBlockedSpeed = (previousVelocity - projectedVelocity).magnitude;

            Vector3 safeNormal = SafeNormal(_scheduledSweepBlockingHit.normal, Vector3.up);
            Vector3 intendedDisplacement = _scheduledSweepState.Direction * _scheduledSweepState.Distance;
            float displacementIntoWall = Vector3.Dot(intendedDisplacement, safeNormal);
            Vector3 projectedDisplacement = intendedDisplacement - (safeNormal * displacementIntoWall);
            if (safeNormal.y < WallSlideTelemetryMaxNormalY)
            {
                float slideAngleDegrees = ResolveProjectionAngleDegrees(intendedDisplacement, projectedDisplacement);
                float velocityReduction01 = ResolveProjectionVelocityReduction01(intendedDisplacement, projectedDisplacement);
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
            Vector3 lossyScale = body.transform.lossyScale;
            float radiusScale = math.max(math.abs(lossyScale.x), math.abs(lossyScale.z));
            float heightScale = math.abs(lossyScale.y);
            radius = math.max(0.01f, capsule.radius * radiusScale - inset);
            float halfHeight = math.max(radius, (capsule.height * heightScale) * 0.5f);
            Vector3 center = body.transform.TransformPoint(capsule.center);
            float segmentHalf = math.max(0f, halfHeight - radius - inset);
            point1 = center + body.transform.up * segmentHalf;
            point2 = center - body.transform.up * segmentHalf;
        }
    }
}
