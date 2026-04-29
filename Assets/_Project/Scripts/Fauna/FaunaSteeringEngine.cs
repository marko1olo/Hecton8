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

        private Transform _selfTransform;
        private FaunaSpeciesProfile _speciesProfile;
        private float _lastBankingRoll;

        public void Init(Rigidbody rb, Transform self, FaunaSpeciesProfile profile = null)
        {
            _selfTransform = self;
            _speciesProfile = profile;
            currentDirection = self.forward;
        }

        /// <summary>
        /// [MIGRATION] Synchronized signature for FaunaBrain.
        /// Handles physical motion, force application, and state-specific speeds.
        /// </summary>
        public void FixedTick(float fdt, Vector3 targetDir, float forceMult, float speedMult, float turnMult, bool isRetreating, Vector3 threatPos = default)
        {
            if (_selfTransform == null) return;

            desiredDirection = targetDir;
            float resolvedForceMultiplier = Mathf.Max(0.1f, forceMult);
            
            // TACTICAL DIRECTION: Predator Retreat (User REQ: Flee strictly from threat)
            if (isRetreating && threatPos != default)
            {
                desiredDirection = (_selfTransform.position - threatPos).normalized;
            }

            // 1. TACTICAL SPEED: Predator Retreat (User REQ: 1.5x speed)
            float stateMod = isRetreating ? (_speciesProfile != null ? _speciesProfile.retreatSpeedMultiplier : 1.5f) : 1f;
            float targetMaxSpeed = moveSpeed * speedMult * stateMod;

            // 2. ACCELERATION / DECELERATION
            float speedTarget = desiredDirection.sqrMagnitude > 0.01f ? targetMaxSpeed : 0f;

            // [REQ] Centripetal Force Limiter: Reduce speed when turning sharply
            if (desiredDirection.sqrMagnitude > 0.01f && _speciesProfile != null && _speciesProfile.centripetalLimit > 0.01f)
            {
                float turnSharpness = 1.0f - Vector3.Dot(_selfTransform.forward, desiredDirection.normalized);
                // 0 = straight, 1 = 90 deg, 2 = 180 deg. Reduce speed target by up to 60% based on limit.
                float drag = Mathf.Clamp01(turnSharpness * _speciesProfile.centripetalLimit * 0.3f);
                speedTarget *= (1.0f - drag);
            }

            currentSpeed = Mathf.Lerp(currentSpeed, speedTarget, acceleration * resolvedForceMultiplier * fdt);

            // 3. DIRECTION & ROTATION
            if (desiredDirection.sqrMagnitude > 0.01f)
            {
                Vector3 lookDir = desiredDirection.normalized;
                Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                
                // Rotation Speed multiplier for aggressive/retreat states
                float rotMod = (isRetreating || speedMult > 1.1f) ? 2.5f * turnMult : turnMult;
                _selfTransform.rotation = Quaternion.Slerp(_selfTransform.rotation, targetRot, rotationSpeed * rotMod * resolvedForceMultiplier * fdt);

                // 4. VISUAL BANKING (User REQ: Fluid aquatic tilt)
                float angle = Vector3.SignedAngle(_selfTransform.forward, lookDir, _selfTransform.up);
                float bankIntensity = _speciesProfile != null ? _speciesProfile.turnBankingIntensity : 1.0f;
                // Heavy banking for retreats (User REQ)
                float bankMult = isRetreating ? 2.0f : 1.0f;
                float targetRoll = -angle * bankingStrength * 0.1f * bankIntensity * bankMult;
                _lastBankingRoll = Mathf.Lerp(_lastBankingRoll, targetRoll, 5f * fdt);

                Vector3 eulers = _selfTransform.rotation.eulerAngles;
                _selfTransform.rotation = Quaternion.Euler(eulers.x, eulers.y, _lastBankingRoll);
            }

            // 5. VELOCITY & POSITION
            velocity = _selfTransform.forward * currentSpeed;
            _selfTransform.position += velocity * fdt;
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
        }
    }
}
