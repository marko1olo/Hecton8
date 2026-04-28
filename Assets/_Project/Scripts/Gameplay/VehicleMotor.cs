using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Kinematic vehicle motor with deferred capsule sweep consumption for mountable transports.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Transport/Vehicle Motor")]
    public sealed class VehicleMotor : MonoBehaviour
    {
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
        private const float DefaultGroundSlopeLimitDegrees = 45f;
        private const float TractionLossStartDegrees = 45f;
        private const float GroundContactHoldSeconds = 0.2f;
        private const float VehicleGravityAcceleration = 9.81f;
        private const float SlopeDot45Degrees = 0.70710678f;
        private const float GroundAlignmentSharpness = 10f;

        private static readonly ProfilerMarker _scheduleProfilerMarker = new ProfilerMarker("H8.VehicleMotor.CapsuleSweep.Schedule");
        private static readonly ProfilerMarker _consumeProfilerMarker = new ProfilerMarker("H8.VehicleMotor.CapsuleSweep.Consume");
        private static readonly ProfilerMarker _driveProfilerMarker = new ProfilerMarker("H8.VehicleMotor.Drive");

        private Rigidbody _body;
        private CapsuleCollider _capsule;
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

        /// <summary>Current kinematic linear velocity in world space.</summary>
        public Vector3 LinearVelocity => _linearVelocity;

        /// <summary>True while a deferred capsule sweep is waiting for consumption.</summary>
        public bool HasPendingSweep => _scheduledSweepPending;

        /// <summary>Returns true when both rigidbody and capsule are available for kinematic sweep driving.</summary>
        public bool IsDriveReady => _body != null && _capsule != null;

        /// <summary>Binds the authoritative rigidbody and sweep capsule.</summary>
        public void Bind(Rigidbody body, CapsuleCollider capsule)
        {
            _body = body;
            _capsule = capsule;
            ResetRuntimeState();
        }

        private void OnDisable()
        {
            DisposeScheduledSweepState();
        }

        private void OnDestroy()
        {
            DisposeScheduledSweepState();
        }

        /// <summary>Clears all accumulated transport motion state.</summary>
        public void ResetRuntimeState()
        {
            _linearVelocity = Vector3.zero;
            _localAngularVelocityDegrees = Vector3.zero;
            _groundNormal = Vector3.up;
            _groundContactTimer = 0f;
        }

        /// <summary>Configures the maximum climbable ground slope before vehicle drive is flattened against world up.</summary>
        public void ConfigureGroundSlopeLimit(float maxSlopeDegrees)
        {
            _groundSlopeLimitDegrees = math.clamp(maxSlopeDegrees, 5f, 89f);
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

            using (_driveProfilerMarker.Auto())
            {
                float safeDeltaTime = math.max(fixedDeltaTime, 0.0001f);

                Vector3 localAngularVelocityDegrees = _localAngularVelocityDegrees;
                localAngularVelocityDegrees.x += (-pitchInput * pitchAngularAccelerationDegrees) * safeDeltaTime;
                localAngularVelocityDegrees.y += (yawInput * yawAngularAccelerationDegrees) * safeDeltaTime;
                float angularDampingFactor = math.saturate(angularDamping * safeDeltaTime);
                localAngularVelocityDegrees = Vector3.Lerp(localAngularVelocityDegrees, Vector3.zero, angularDampingFactor);
                _localAngularVelocityDegrees = HectonPlayerMotor.SafeVelocity(localAngularVelocityDegrees);

                Quaternion deltaRotation = Quaternion.Euler(_localAngularVelocityDegrees * safeDeltaTime);
                Quaternion targetRotation = _body.rotation * deltaRotation;
                targetRotation = ResolveGroundAlignedRotation(targetRotation, safeDeltaTime);
                _body.MoveRotation(targetRotation);

                float clampedForwardInput = math.clamp(forwardInput, -1f, 1f);
                Vector3 targetForward = targetRotation * Vector3.forward;
                EvaluateSlopeTraction(targetForward, safeDeltaTime, out float tractionMultiplier, out float downwardAcceleration);
                float effectiveAcceleration = math.max(0f, thrustAcceleration) * tractionMultiplier * clampedForwardInput;
                Vector3 candidateVelocity = _linearVelocity + (targetForward * effectiveAcceleration * safeDeltaTime);
                if (downwardAcceleration > 0f)
                    candidateVelocity += Vector3.down * (downwardAcceleration * safeDeltaTime);

                float dampingDenominator = math.max(1f, 1f + math.max(0f, linearDamping) * safeDeltaTime);
                candidateVelocity /= dampingDenominator;

                float safeMaxSpeed = math.max(0.1f, maxSpeed);
                float sqrMagnitude = candidateVelocity.sqrMagnitude;
                if (sqrMagnitude > (safeMaxSpeed * safeMaxSpeed))
                    candidateVelocity = candidateVelocity.normalized * safeMaxSpeed;

                _linearVelocity = HectonPlayerMotor.SafeVelocity(candidateVelocity);
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
            if (!_scheduledSweepPending || !_scheduledSweepHandle.IsCompleted)
                return false;

            using (_consumeProfilerMarker.Auto())
            {
                _scheduledSweepHandle.Complete();
                _scheduledSweepHandle = default;
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
                Vector3 projectedVelocity = Vector3.ProjectOnPlane(_linearVelocity, blockingHit.normal);
                if (IsSlopeTooSteep(blockingHit.normal))
                    projectedVelocity = Vector3.ProjectOnPlane(projectedVelocity, Vector3.up);

                _linearVelocity = projectedVelocity;
                _linearVelocity = HectonPlayerMotor.SafeVelocity(_linearVelocity);
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

#pragma warning disable CS0618
        private static int GetHitColliderInstanceId(in RaycastHit hit)
        {
            return hit.colliderInstanceID;
        }
#pragma warning restore CS0618

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
