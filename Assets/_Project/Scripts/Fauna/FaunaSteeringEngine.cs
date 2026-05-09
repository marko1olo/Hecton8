using Hecton8.Physics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hecton8.AI
{
    /// <summary>
    /// Handles physical aquatic movement and visual banking for HECTON-8 Fauna.
    /// [RULE] ZERO GC. NO Time.deltaTime (use dt).
    /// </summary>
    [System.Serializable]
    public class FaunaSteeringEngine
    {
        [Header("── Configuration ────────────────────────────────")]
        public float moveSpeed = 5f;
        [FormerlySerializedAs("maxSpeed")]
        public float maxSpeed = 5f; 
        [FormerlySerializedAs("swimForce")]
        public float swimForce = 15f; 
        public float acceleration = 2f;
        [FormerlySerializedAs("rotationSpeed")]
        public float rotationSpeed = 3f;
        [FormerlySerializedAs("turnSpeed")]
        public float turnSpeed = 3f; 
        public float bankingStrength = 25f;
        public float stopDistance = 0.5f;

        [Header("── State ──────────────────────────────────────")]
        public Vector3 velocity;
        public Vector3 currentDirection;
        public Vector3 desiredDirection;
        public float currentSpeed;

        private Rigidbody _body;
        private Transform _selfTransform;
        private FaunaSpeciesProfile _speciesProfile;
        private float _lastBankingRoll;
        private Vector3 _smoothedSteerDirection;

        private static readonly Quaternion _ForwardRotation = new Quaternion(0f, 0f, 0f, 1f);
        private static readonly Quaternion _BackRotation = new Quaternion(0f, 1f, 0f, 0f);
        private static readonly Quaternion _RightRotation = new Quaternion(0f, 0.70710678f, 0f, 0.70710678f);
        private static readonly Quaternion _LeftRotation = new Quaternion(0f, -0.70710678f, 0f, 0.70710678f);
        private static readonly Quaternion _UpRotation = new Quaternion(-0.70710678f, 0f, 0f, 0.70710678f);
        private static readonly Quaternion _DownRotation = new Quaternion(0.70710678f, 0f, 0f, 0.70710678f);

        public void Init(Rigidbody rb, Transform self, FaunaSpeciesProfile profile = null)
        {
            _body = rb;
            _selfTransform = self;
            _speciesProfile = profile;
            currentDirection = self.forward;
            _smoothedSteerDirection = ResolveDominantAxisDirection(currentDirection, Vector3.forward);
            velocity = rb != null ? rb.linearVelocity : Vector3.zero;
            currentSpeed = ApproximateMagnitude(velocity);
        }

        /// <summary>
        /// [MIGRATION] Synchronized signature for FaunaBrain.
        /// Handles physical motion, force application, and state-specific speeds.
        /// </summary>
        public void FixedTick(float fdt, Vector3 targetDir, float forceMult, float speedMult, float turnMult, bool isRetreating, Vector3 threatPos = default)
        {
            if (_selfTransform == null || _body == null || fdt <= 0f)
                return;

            desiredDirection = targetDir;
            float resolvedForceMultiplier = math.max(0.1f, forceMult);
            
            // TACTICAL DIRECTION: Predator Retreat (User REQ: Flee strictly from threat)
            if (isRetreating && threatPos != default)
            {
                Vector3 retreatDirection = _body.position - threatPos;
                if (retreatDirection.sqrMagnitude > 0.0001f)
                    desiredDirection = ResolveDominantAxisDirection(retreatDirection, desiredDirection);
            }

            Vector3 fallbackForward = _smoothedSteerDirection.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(_smoothedSteerDirection, Vector3.forward)
                : (_body.rotation * Vector3.forward);
            if (fallbackForward.sqrMagnitude <= 0.0001f)
                fallbackForward = Vector3.forward;

            Vector3 steeringTarget = desiredDirection.sqrMagnitude > 0.0001f
                ? ResolveDominantAxisDirection(desiredDirection, fallbackForward)
                : ResolveDominantAxisDirection(fallbackForward, Vector3.forward);
            float steeringSharpness = math.max(0.01f, turnSpeed * math.max(0.1f, turnMult) * resolvedForceMultiplier);
            _smoothedSteerDirection = (Vector3)HectonContactJob.ResolveSteeringArc(
                fallbackForward,
                steeringTarget,
                fdt,
                steeringSharpness);

            // 1. TACTICAL SPEED: Predator Retreat (User REQ: 1.5x speed)
            float stateMod = isRetreating ? (_speciesProfile != null ? _speciesProfile.retreatSpeedMultiplier : 1.5f) : 1f;
            float targetMaxSpeed = math.max(0.1f, maxSpeed * speedMult * stateMod);

            // 2. ACCELERATION / DECELERATION
            float speedTarget = steeringTarget.sqrMagnitude > 0.01f ? targetMaxSpeed : 0f;

            // [REQ] Centripetal Force Limiter: Reduce speed when turning sharply
            if (steeringTarget.sqrMagnitude > 0.01f && _speciesProfile != null && _speciesProfile.centripetalLimit > 0.01f)
            {
                Vector3 currentForward = _body.rotation * Vector3.forward;
                float turnSharpness = 1.0f - Vector3.Dot(currentForward, steeringTarget);
                // 0 = straight, 1 = 90 deg, 2 = 180 deg. Reduce speed target by up to 60% based on limit.
                float drag = math.saturate(turnSharpness * _speciesProfile.centripetalLimit * 0.3f);
                speedTarget *= (1.0f - drag);
            }

            Vector3 currentVelocity = _body.linearVelocity;
            float maxVelocityDelta = math.max(0.01f, swimForce * resolvedForceMultiplier * fdt);
            if (_smoothedSteerDirection.sqrMagnitude > 0.01f)
            {
                Vector3 desiredVelocity = ResolveDominantAxisDirection(_smoothedSteerDirection, steeringTarget) * speedTarget;
                currentVelocity = MoveTowardsApprox(currentVelocity, desiredVelocity, maxVelocityDelta);
            }
            else
            {
                currentVelocity = MoveTowardsApprox(currentVelocity, Vector3.zero, maxVelocityDelta);
            }

            if (!IsFinite(currentVelocity))
                currentVelocity = Vector3.zero;

            _body.linearVelocity = currentVelocity;
            velocity = currentVelocity;
            currentSpeed = ApproximateMagnitude(currentVelocity);

            // 3. DIRECTION & ROTATION
            Vector3 facingDirection = currentVelocity.sqrMagnitude > 0.01f
                ? ResolveDominantAxisDirection(currentVelocity, currentDirection)
                : currentDirection;
            if (facingDirection.sqrMagnitude > 0.01f)
            {
                Vector3 lookDir = ResolveDominantAxisDirection(facingDirection, Vector3.forward);
                Quaternion targetRot = ResolveDominantAxisRotation(lookDir);
                
                // Rotation Speed multiplier for aggressive/retreat states
                float rotMod = (isRetreating || speedMult > 1.1f) ? 2.5f * turnMult : turnMult;
                Quaternion nextRotation = FastNlerp(_body.rotation, targetRot, math.saturate(turnSpeed * rotMod * resolvedForceMultiplier * fdt));

                // 4. VISUAL BANKING (User REQ: Fluid aquatic tilt)
                float lateralTurn = Vector3.Dot(nextRotation * Vector3.right, lookDir);
                float bankIntensity = _speciesProfile != null ? _speciesProfile.turnBankingIntensity : 1.0f;
                // Heavy banking for retreats (User REQ)
                float bankMult = isRetreating ? 2.0f : 1.0f;
                float targetRoll = -lateralTurn * bankingStrength * bankIntensity * bankMult;
                _lastBankingRoll = math.lerp(_lastBankingRoll, targetRoll, math.saturate(5f * fdt));

                Quaternion bankedRotation = NormalizeQuaternion(nextRotation * ResolveRollApprox(_lastBankingRoll));
                if (IsFinite(bankedRotation))
                    _body.MoveRotation(bankedRotation);
                currentDirection = lookDir;
            }
        }

        /// <summary>
        /// Legacy bridge for simple movement.
        /// </summary>
        public void ApplySteering(float dt, FaunaBrain.AIState state)
        {
            FixedTick(dt, desiredDirection, 1f, 1f, 1f, state == FaunaBrain.AIState.Retreat || state == FaunaBrain.AIState.ApexForcedRetreat);
        }

        public void Stop()
        {
            currentSpeed = 0f;
            velocity = Vector3.zero;
            desiredDirection = Vector3.zero;
            _smoothedSteerDirection = currentDirection.sqrMagnitude > 0.0001f ? currentDirection : Vector3.forward;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }

        private static Vector3 ResolveDominantAxisDirection(Vector3 direction, Vector3 fallback)
        {
            if (!IsFinite(direction) || direction.sqrMagnitude <= 0.0001f)
                direction = fallback;

            if (!IsFinite(direction) || direction.sqrMagnitude <= 0.0001f)
                return Vector3.forward;

            float absX = math.abs(direction.x);
            float absY = math.abs(direction.y);
            float absZ = math.abs(direction.z);
            if (absX >= absY && absX >= absZ)
                return direction.x < 0f ? Vector3.left : Vector3.right;

            if (absY >= absZ)
                return direction.y < 0f ? Vector3.down : Vector3.up;

            return direction.z < 0f ? Vector3.back : Vector3.forward;
        }

        private static float ApproximateMagnitude(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + mid * 0.375f + min * 0.125f;
        }

        private static Vector3 MoveTowardsApprox(Vector3 current, Vector3 target, float maxDelta)
        {
            Vector3 delta = target - current;
            float distance = ApproximateMagnitude(delta);
            if (distance <= maxDelta || distance <= 0.0001f)
                return target;

            return current + delta * (maxDelta / distance);
        }

        private static Quaternion ResolveDominantAxisRotation(Vector3 axis)
        {
            float absX = math.abs(axis.x);
            float absY = math.abs(axis.y);
            float absZ = math.abs(axis.z);
            if (absX >= absY && absX >= absZ)
                return axis.x < 0f ? _LeftRotation : _RightRotation;

            if (absY >= absZ)
                return axis.y < 0f ? _DownRotation : _UpRotation;

            return axis.z < 0f ? _BackRotation : _ForwardRotation;
        }

        private static Quaternion FastNlerp(Quaternion from, Quaternion to, float t)
        {
            float dot = from.x * to.x + from.y * to.y + from.z * to.z + from.w * to.w;
            if (dot < 0f)
            {
                to.x = -to.x;
                to.y = -to.y;
                to.z = -to.z;
                to.w = -to.w;
            }

            Quaternion blended = new Quaternion(
                math.lerp(from.x, to.x, t),
                math.lerp(from.y, to.y, t),
                math.lerp(from.z, to.z, t),
                math.lerp(from.w, to.w, t));
            return NormalizeQuaternion(blended);
        }

        private static Quaternion ResolveRollApprox(float rollDegrees)
        {
            float halfRadians = rollDegrees * 0.008726646f;
            float halfSq = halfRadians * halfRadians;
            float sinHalf = halfRadians * (1f - halfSq * 0.16666667f);
            float cosHalf = 1f - halfSq * 0.5f + halfSq * halfSq * 0.04166667f;
            return NormalizeQuaternion(new Quaternion(0f, 0f, sinHalf, cosHalf));
        }

        private static Quaternion NormalizeQuaternion(Quaternion value)
        {
            float lengthSq = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
            if (lengthSq <= 0.000001f || !float.IsFinite(lengthSq))
                return _ForwardRotation;

            float invLength = math.rsqrt(lengthSq);
            value.x *= invLength;
            value.y *= invLength;
            value.z *= invLength;
            value.w *= invLength;
            return value;
        }
    }
}
