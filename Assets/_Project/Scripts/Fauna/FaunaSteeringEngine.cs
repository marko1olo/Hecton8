using Hecton8.Physics;
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

        public void Init(Rigidbody rb, Transform self, FaunaSpeciesProfile profile = null)
        {
            _body = rb;
            _selfTransform = self;
            _speciesProfile = profile;
            currentDirection = self.forward;
            _smoothedSteerDirection = currentDirection.sqrMagnitude > 0.0001f ? currentDirection.normalized : Vector3.forward;
            velocity = rb != null ? rb.linearVelocity : Vector3.zero;
            currentSpeed = velocity.magnitude;
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
            float resolvedForceMultiplier = Mathf.Max(0.1f, forceMult);
            
            // TACTICAL DIRECTION: Predator Retreat (User REQ: Flee strictly from threat)
            if (isRetreating && threatPos != default)
            {
                Vector3 retreatDirection = _body.position - threatPos;
                if (retreatDirection.sqrMagnitude > 0.0001f)
                    desiredDirection = retreatDirection.normalized;
            }

            Vector3 fallbackForward = _smoothedSteerDirection.sqrMagnitude > 0.0001f
                ? _smoothedSteerDirection
                : (_body.rotation * Vector3.forward);
            if (fallbackForward.sqrMagnitude <= 0.0001f)
                fallbackForward = Vector3.forward;

            Vector3 steeringTarget = desiredDirection.sqrMagnitude > 0.0001f
                ? desiredDirection.normalized
                : fallbackForward.normalized;
            float steeringSharpness = Mathf.Max(0.01f, turnSpeed * Mathf.Max(0.1f, turnMult) * resolvedForceMultiplier);
            _smoothedSteerDirection = (Vector3)HectonContactJob.ResolveSteeringArc(
                fallbackForward,
                steeringTarget,
                fdt,
                steeringSharpness);

            // 1. TACTICAL SPEED: Predator Retreat (User REQ: 1.5x speed)
            float stateMod = isRetreating ? (_speciesProfile != null ? _speciesProfile.retreatSpeedMultiplier : 1.5f) : 1f;
            float targetMaxSpeed = Mathf.Max(0.1f, maxSpeed * speedMult * stateMod);

            // 2. ACCELERATION / DECELERATION
            float speedTarget = steeringTarget.sqrMagnitude > 0.01f ? targetMaxSpeed : 0f;

            // [REQ] Centripetal Force Limiter: Reduce speed when turning sharply
            if (steeringTarget.sqrMagnitude > 0.01f && _speciesProfile != null && _speciesProfile.centripetalLimit > 0.01f)
            {
                Vector3 currentForward = _body.rotation * Vector3.forward;
                float turnSharpness = 1.0f - Vector3.Dot(currentForward, steeringTarget);
                // 0 = straight, 1 = 90 deg, 2 = 180 deg. Reduce speed target by up to 60% based on limit.
                float drag = Mathf.Clamp01(turnSharpness * _speciesProfile.centripetalLimit * 0.3f);
                speedTarget *= (1.0f - drag);
            }

            Vector3 currentVelocity = _body.linearVelocity;
            float maxVelocityDelta = Mathf.Max(0.01f, swimForce * resolvedForceMultiplier * fdt);
            if (_smoothedSteerDirection.sqrMagnitude > 0.01f)
            {
                Vector3 desiredVelocity = _smoothedSteerDirection.normalized * speedTarget;
                currentVelocity = Vector3.MoveTowards(currentVelocity, desiredVelocity, maxVelocityDelta);
            }
            else
            {
                currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, maxVelocityDelta);
            }

            if (!IsFinite(currentVelocity))
                currentVelocity = Vector3.zero;

            _body.linearVelocity = currentVelocity;
            velocity = currentVelocity;
            currentSpeed = currentVelocity.magnitude;

            // 3. DIRECTION & ROTATION
            Vector3 facingDirection = currentVelocity.sqrMagnitude > 0.01f ? currentVelocity.normalized : currentDirection;
            if (facingDirection.sqrMagnitude > 0.01f)
            {
                Vector3 lookDir = facingDirection.normalized;
                Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                
                // Rotation Speed multiplier for aggressive/retreat states
                float rotMod = (isRetreating || speedMult > 1.1f) ? 2.5f * turnMult : turnMult;
                Quaternion nextRotation = Quaternion.Slerp(_body.rotation, targetRot, turnSpeed * rotMod * resolvedForceMultiplier * fdt);

                // 4. VISUAL BANKING (User REQ: Fluid aquatic tilt)
                float angle = Vector3.SignedAngle(_selfTransform.forward, lookDir, _selfTransform.up);
                float bankIntensity = _speciesProfile != null ? _speciesProfile.turnBankingIntensity : 1.0f;
                // Heavy banking for retreats (User REQ)
                float bankMult = isRetreating ? 2.0f : 1.0f;
                float targetRoll = -angle * bankingStrength * 0.1f * bankIntensity * bankMult;
                _lastBankingRoll = Mathf.Lerp(_lastBankingRoll, targetRoll, 5f * fdt);

                Quaternion bankedRotation = nextRotation * Quaternion.AngleAxis(_lastBankingRoll, Vector3.forward);
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
            FixedTick(dt, desiredDirection, 1f, 1f, 1f, state == FaunaBrain.AIState.Retreat);
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
    }
}
