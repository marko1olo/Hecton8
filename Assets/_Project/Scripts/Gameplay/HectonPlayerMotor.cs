using Hecton8.Core;
using Hecton8.Physics;
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
    public sealed class HectonPlayerMotor : MonoBehaviour, IMotorForces, IPostFixedTickable
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
        private const float MaxHydrodynamicGhostBlend = 0.15f;

        private static readonly ProfilerMarker _scheduledSweepProfilerMarker = new ProfilerMarker("H8.PlayerMotor.CapsuleSweep.Schedule");
        private static readonly ProfilerMarker _scheduledSweepConsumeProfilerMarker = new ProfilerMarker("H8.PlayerMotor.CapsuleSweep.Consume");
        private static readonly ProfilerMarker _hydrodynamicGhostProfilerMarker = new ProfilerMarker("H8.PlayerMotor.HydrodynamicGhost.Resolve");

        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private bool _isGrounded;
        private readonly RaycastHit[] _sweepHitBuffer = new RaycastHit[32]; // COLD ALLOC: RaycastHit[32] — motor-owned sweep/collision query buffer for kinematic isolation — owner: HectonPlayerMotor
        private NativeArray<float3> _hydrodynamicGhostVelocityHistory;
        private NativeArray<CapsulecastCommand> _scheduledSweepCommands;
        private NativeArray<RaycastHit> _scheduledSweepResults;
        private JobHandle _scheduledSweepHandle;
        private bool _scheduledSweepPending;
        private bool _scheduledSweepResultReady;
        private bool _scheduledSweepWasBlocked;
        private ScheduledSweepState _scheduledSweepState;
        private RaycastHit _scheduledSweepBlockingHit;
        private Vector3 _scheduledSweepResolvedPosition;
        private float _scheduledSweepBlockedSpeed;
        private int _hydrodynamicGhostWriteIndex;
        private int _hydrodynamicGhostSampleCount;
        private bool _registeredPostFixedTick;

        /// <inheritdoc />
        public Rigidbody Body => _body;

        /// <inheritdoc />
        public CapsuleCollider Capsule => _capsule;

        /// <inheritdoc />
        public bool IsGrounded => _isGrounded;

        /// <summary>Motor-owned sweep buffer. Reserved for kinematic queries only.</summary>
        public RaycastHit[] SweepHitBuffer => _sweepHitBuffer;

        /// <summary>Binds authoritative body references owned by the locomotion controller.</summary>
        public void Bind(Rigidbody body, CapsuleCollider capsule)
        {
            _body = body;
            _capsule = capsule;
            EnsureHydrodynamicGhostState();
            ResetHydrodynamicAddedMassState();
        }

        private void OnEnable()
        {
            TryRegisterPostFixedTick();
        }

        private void OnDisable()
        {
            TryUnregisterPostFixedTick();
            DisposeScheduledSweepState();
            DisposeHydrodynamicGhostState();
        }

        private void OnDestroy()
        {
            TryUnregisterPostFixedTick();
            DisposeScheduledSweepState();
            DisposeHydrodynamicGhostState();
        }

        /// <summary>Updates grounded state mirror for external systems.</summary>
        public void SetGroundedState(bool isGrounded)
        {
            _isGrounded = isGrounded;
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

            Vector3 projectedVelocity = Vector3.ProjectOnPlane(_body.linearVelocity, planeNormal);
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
        /// Applies a sweep-gated move and removes velocity along the blocking surface normal.
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
        /// Applies a sweep-gated move using caller-supplied capsule metrics to avoid transform traversal in hot paths.
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

            Vector3 currentPosition = _body.position;
            Vector3 currentPoint1 = capsulePoint1;
            Vector3 currentPoint2 = capsulePoint2;
            Vector3 remainingDisplacement = displacement;
            Vector3 accumulatedDisplacement = Vector3.zero;
            bool blocked = false;
            blockingHit = default;

            for (int iteration = 0; iteration < MaxSlideSweepIterations; iteration++)
            {
                float remainingDistance = remainingDisplacement.magnitude;
                if (remainingDistance <= 0.0001f)
                    break;

                Vector3 direction = remainingDisplacement / remainingDistance;
                if (!TryFindNearestBlockingHit(
                        currentPoint1,
                        currentPoint2,
                        capsuleRadius,
                        direction,
                        remainingDistance + skinWidth,
                        layerMask,
                        selfColliderInstanceId,
                        out RaycastHit nearestHit))
                {
                    accumulatedDisplacement += remainingDisplacement;
                    currentPosition += remainingDisplacement;
                    break;
                }

                blocked = true;
                if (blockingHit.collider == null)
                    blockingHit = nearestHit;

                float safeDistance = math.max(0f, nearestHit.distance - skinWidth);
                Vector3 advance = direction * safeDistance;
                if (advance.sqrMagnitude > MinVectorMagnitudeSq)
                {
                    accumulatedDisplacement += advance;
                    currentPosition += advance;
                }

                float penetrationDepth = math.max(0f, skinWidth - nearestHit.distance);
                if (penetrationDepth > 0f)
                {
                    Vector3 depenetration = nearestHit.normal * penetrationDepth;
                    accumulatedDisplacement += depenetration;
                    currentPosition += depenetration;
                }

                Vector3 remainingAfterAdvance = remainingDisplacement - advance;
                Vector3 safeNormal = SafeNormal(nearestHit.normal, Vector3.up);
                float displacementIntoWall = Vector3.Dot(remainingAfterAdvance, safeNormal);
                Vector3 slideDisplacement = remainingAfterAdvance - (safeNormal * displacementIntoWall);
                if (slideDisplacement.sqrMagnitude <= MinVectorMagnitudeSq)
                    break;

                Vector3 capsuleOffset = currentPosition - _body.position;
                currentPoint1 = capsulePoint1 + capsuleOffset;
                currentPoint2 = capsulePoint2 + capsuleOffset;
                remainingDisplacement = slideDisplacement;
            }

            resolvedPosition = _body.position + accumulatedDisplacement;
            MovePosition(resolvedPosition);

            if (!blocked)
                return true;

            ProjectLinearVelocityOnPlane(blockingHit.normal);
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
            if (speedMag <= 0.0001f)
                return velocity;

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
            if (speed <= 0.0001f)
                return velocity;

            float safeDt = math.max(dt, 0.0001f);
            float denominator = 1f + math.max(0f, dragCoefficient) * speed * safeDt;
            return SafeVelocity(velocity / math.max(denominator, 0.001f), velocity);
        }

        /// <summary>Clears transient runtime state.</summary>
        public void ResetRuntimeState()
        {
            _isGrounded = false;
            ResetHydrodynamicAddedMassState();
        }

        /// <summary>
        /// Returns a hydrodynamically weighted perceived velocity using a 3-frame inertial ghost.
        /// Ghost contribution scales with the requested submersion factor so dry-space KCC sweeps stay immediate.
        /// </summary>
        public Vector3 ResolveHydrodynamicAddedMassVelocity(Vector3 actualVelocity, float submersionFactor)
        {
            Vector3 safeActualVelocity = SafeVelocity(actualVelocity);
            float clampedSubmersionFactor = math.saturate(submersionFactor);
            float ghostBlend = MaxHydrodynamicGhostBlend * clampedSubmersionFactor;
            if (ghostBlend <= 0.0001f)
            {
                ResetHydrodynamicAddedMassState();
                return safeActualVelocity;
            }

            using (_hydrodynamicGhostProfilerMarker.Auto())
            {
                RecordHydrodynamicGhostVelocity(safeActualVelocity);
                if (_hydrodynamicGhostSampleCount < _hydrodynamicGhostVelocityHistory.Length)
                    return safeActualVelocity;

                float3 oldestVelocity = _hydrodynamicGhostVelocityHistory[_hydrodynamicGhostWriteIndex];
                float3 currentVelocity = new float3(safeActualVelocity.x, safeActualVelocity.y, safeActualVelocity.z);
                float3 perceivedVelocity = math.lerp(currentVelocity, oldestVelocity, ghostBlend);
                return SafeVelocity(new Vector3(perceivedVelocity.x, perceivedVelocity.y, perceivedVelocity.z), safeActualVelocity);
            }
        }

        /// <summary>Clears the persistent underwater added-mass history.</summary>
        public void ResetHydrodynamicAddedMassState()
        {
            EnsureHydrodynamicGhostState();
            _hydrodynamicGhostWriteIndex = 0;
            _hydrodynamicGhostSampleCount = 0;
            for (int i = 0; i < _hydrodynamicGhostVelocityHistory.Length; i++)
                _hydrodynamicGhostVelocityHistory[i] = float3.zero;
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

            _scheduledSweepCommands[0] = new CapsulecastCommand(
                capsulePoint1,
                capsulePoint2,
                math.max(0.01f, capsuleRadius),
                direction,
                new QueryParameters(layerMask, false, QueryTriggerInteraction.Ignore),
                distance + skinWidth);

            using (_scheduledSweepProfilerMarker.Auto())
            {
                _scheduledSweepHandle = CapsulecastCommand.ScheduleBatch(
                    _scheduledSweepCommands,
                    _scheduledSweepResults,
                    1,
                    ScheduledSweepMaxHits,
                    default);
                _scheduledSweepPending = true;
            }

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
            CompleteScheduledSweepInPostFixedSwapWindow();
        }

        private static bool IsFiniteNonZero(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3)) && math.lengthsq(value3) > MinVectorMagnitudeSq;
        }

        private static int GetHitColliderInstanceId(in RaycastHit hit)
        {
            return unchecked((int)EntityId.ToULong(hit.colliderEntityId));
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
            int hitCount = UnityEngine.Physics.CapsuleCastNonAlloc(
                capsulePoint1,
                capsulePoint2,
                math.max(0.01f, capsuleRadius),
                direction,
                _sweepHitBuffer,
                distance,
                layerMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.MaxValue;
            int nearestIndex = -1;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _sweepHitBuffer[i];
                int hitColliderInstanceId = GetHitColliderInstanceId(in hit);
                if (hitColliderInstanceId == 0 || hitColliderInstanceId == selfColliderInstanceId)
                    continue;

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestIndex = i;
                }
            }

            if (nearestIndex < 0)
                return false;

            nearestHit = _sweepHitBuffer[nearestIndex];
            return true;
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

        private void EnsureScheduledSweepState()
        {
            if (!_scheduledSweepCommands.IsCreated)
                _scheduledSweepCommands = new NativeArray<CapsulecastCommand>(ScheduledSweepCommandCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            if (!_scheduledSweepResults.IsCreated)
                _scheduledSweepResults = new NativeArray<RaycastHit>(ScheduledSweepMaxHits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private void DisposeScheduledSweepState()
        {
            if (_scheduledSweepCommands.IsCreated)
            {
                JobHandle dependency = _scheduledSweepPending ? _scheduledSweepHandle : default;
                dependency = _scheduledSweepCommands.Dispose(dependency);
                _scheduledSweepCommands = default;
                if (_scheduledSweepResults.IsCreated)
                {
                    dependency = _scheduledSweepResults.Dispose(dependency);
                    _scheduledSweepResults = default;
                }

                if (dependency.IsCompleted)
                    dependency.Complete();
            }
            else if (_scheduledSweepResults.IsCreated)
            {
                _scheduledSweepResults.Dispose();
                _scheduledSweepResults = default;
            }

            _scheduledSweepPending = false;
            _scheduledSweepHandle = default;
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
            _registeredPostFixedTick = true;
        }

        private void TryUnregisterPostFixedTick()
        {
            if (!_registeredPostFixedTick)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Player);
            _registeredPostFixedTick = false;
        }

        private void CompleteScheduledSweepInPostFixedSwapWindow()
        {
            if (!_scheduledSweepPending || !_scheduledSweepHandle.IsCompleted)
                return;

            _scheduledSweepHandle.Complete();
            _scheduledSweepPending = false;
            _scheduledSweepHandle = default;
            _scheduledSweepResultReady = true;
            _scheduledSweepWasBlocked = false;
            _scheduledSweepBlockingHit = default;
            _scheduledSweepBlockedSpeed = 0f;
            _scheduledSweepResolvedPosition = _body != null ? _body.position : _scheduledSweepState.StartPosition;

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
                return;

            _scheduledSweepWasBlocked = true;
            _scheduledSweepBlockingHit = _scheduledSweepResults[nearestIndex];
            float safeDistance = math.max(0f, _scheduledSweepBlockingHit.distance - _scheduledSweepState.SkinWidth);
            float penetrationDepth = math.max(0f, _scheduledSweepState.SkinWidth - _scheduledSweepBlockingHit.distance);
            _scheduledSweepResolvedPosition = _scheduledSweepState.StartPosition +
                (_scheduledSweepState.Direction * safeDistance) +
                (_scheduledSweepBlockingHit.normal * penetrationDepth);

            Vector3 previousVelocity = _body != null ? _body.linearVelocity : Vector3.zero;
            Vector3 safeNormal = SafeNormal(_scheduledSweepBlockingHit.normal, Vector3.up);
            Vector3 projectedVelocity = previousVelocity - (safeNormal * Vector3.Dot(previousVelocity, safeNormal));
            _scheduledSweepBlockedSpeed = (previousVelocity - projectedVelocity).magnitude;

            MovePosition(_scheduledSweepResolvedPosition);
            SetLinearVelocity(projectedVelocity);
        }

        private void RecordHydrodynamicGhostVelocity(Vector3 velocity)
        {
            EnsureHydrodynamicGhostState();
            Vector3 safeVelocity = SafeVelocity(velocity);
            _hydrodynamicGhostVelocityHistory[_hydrodynamicGhostWriteIndex] = new float3(safeVelocity.x, safeVelocity.y, safeVelocity.z);
            _hydrodynamicGhostWriteIndex = (_hydrodynamicGhostWriteIndex + 1) % _hydrodynamicGhostVelocityHistory.Length;
            if (_hydrodynamicGhostSampleCount < _hydrodynamicGhostVelocityHistory.Length)
                _hydrodynamicGhostSampleCount++;
        }

        private void EnsureHydrodynamicGhostState()
        {
            if (_hydrodynamicGhostVelocityHistory.IsCreated)
                return;

            // COLD ALLOC: NativeArray<float3>[4] — 3-frame added-mass inertial ghost history for underwater locomotion perception — owner: HectonPlayerMotor
            _hydrodynamicGhostVelocityHistory = new NativeArray<float3>(4, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private void DisposeHydrodynamicGhostState()
        {
            if (!_hydrodynamicGhostVelocityHistory.IsCreated)
                return;

            _hydrodynamicGhostVelocityHistory.Dispose();
            _hydrodynamicGhostVelocityHistory = default;
            _hydrodynamicGhostWriteIndex = 0;
            _hydrodynamicGhostSampleCount = 0;
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
