using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Authoritative kinematic application layer for player locomotion.
    /// All Rigidbody writes route through this component.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player/Hecton Player Motor")]
    public sealed class HectonPlayerMotor : MonoBehaviour, IMotorForces
    {
        private const float MinVectorMagnitudeSq = 0.000001f;

        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private bool _isGrounded;
        private readonly RaycastHit[] _sweepHitBuffer = new RaycastHit[32]; // COLD ALLOC: RaycastHit[32] — motor-owned sweep/collision query buffer for kinematic isolation — owner: HectonPlayerMotor

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

            _body.AddForce(force, ForceMode.Force);
        }

        /// <summary>Applies a world-space acceleration after finite validation.</summary>
        public void ApplyAcceleration(Vector3 acceleration)
        {
            if (_body == null || !IsFiniteNonZero(acceleration))
                return;

            _body.AddForce(acceleration, ForceMode.Acceleration);
        }

        /// <summary>Applies a world-space velocity change after finite validation.</summary>
        public void ApplyVelocityChange(Vector3 velocityChange)
        {
            if (_body == null || !IsFiniteNonZero(velocityChange))
                return;

            _body.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        /// <summary>Applies an impulse after finite validation.</summary>
        public void ApplyImpulse(Vector3 impulse)
        {
            if (_body == null || !IsFiniteNonZero(impulse))
                return;

            _body.AddForce(impulse, ForceMode.Impulse);
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

            _body.AddTorque(clampedTorque, ForceMode.Force);
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

            _body.AddTorque(clampedDelta, ForceMode.VelocityChange);
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

            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            if (!math.all(math.isfinite(velocity3)))
                return;

            _body.linearVelocity = velocity;
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
            blockingHit = default;
            if (_body == null || _capsule == null)
                return false;

            if (!IsFiniteNonZero(displacement))
                return true;

            float distance = displacement.magnitude;
            if (distance <= 0.0001f)
                return true;

            Vector3 direction = displacement / distance;
            ResolveCapsulePoints(_body, _capsule, 0f, out Vector3 point1, out Vector3 point2, out float radius);
            int hitCount = UnityEngine.Physics.CapsuleCastNonAlloc(
                point1,
                point2,
                radius,
                direction,
                _sweepHitBuffer,
                distance + skinWidth,
                layerMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.MaxValue;
            int nearestIndex = -1;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _sweepHitBuffer[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.attachedRigidbody == _body)
                    continue;

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestIndex = i;
                }
            }

            if (nearestIndex < 0)
            {
                MovePosition(_body.position + displacement);
                return true;
            }

            blockingHit = _sweepHitBuffer[nearestIndex];
            float safeDistance = math.max(0f, blockingHit.distance - skinWidth);
            if (safeDistance > 0.0001f)
                MovePosition(_body.position + direction * safeDistance);

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
            float safeDt = math.max(dt, 0.0001f);
            float3 speed = math.abs(velocity3);
            float3 denominator = 1f + drag3 * speed * safeDt;
            float3 result = velocity3 / math.max(denominator, new float3(0.001f));
            return new Vector3(result.x, result.y, result.z);
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
            return velocity / math.max(denominator, 0.001f);
        }

        /// <summary>Clears transient runtime state.</summary>
        public void ResetRuntimeState()
        {
            _isGrounded = false;
        }

        private static bool IsFiniteNonZero(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3)) && math.lengthsq(value3) > MinVectorMagnitudeSq;
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
