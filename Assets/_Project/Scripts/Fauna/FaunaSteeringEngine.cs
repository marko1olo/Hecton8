using Hecton8.Core;
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
        private const float ApexSteeringResponseScale = 0.55f;
        private const float ApexBankResponse = 2.75f;
        private const float SwarmBankResponse = 5f;
        private const float MinDirectionSqr = 0.0001f;
        private const float MinQuaternionLengthSq = 0.000001f;
        [Header("Configuration")]
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

        [Header("State")]
        public Vector3 velocity;
        public Vector3 currentDirection;
        public Vector3 desiredDirection;
        public float currentSpeed;

        private Rigidbody _body;
        private Transform _selfTransform;
        private FaunaSpeciesProfile _speciesProfile;
        private IPhysicsService _physicsService;
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
            _physicsService = GlobalRegistry.Physics;
            currentDirection = self.forward;
            _smoothedSteerDirection = ResolveDominantAxisDirection(currentDirection, Vector3.forward);
            velocity = rb != null ? rb.linearVelocity : Vector3.zero;
            currentSpeed = ResolveSpeedBucket(velocity.sqrMagnitude);
        }

        public void BindPhysicsService(IPhysicsService physicsService)
        {
            _physicsService = physicsService;
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
            bool useApexSteering = UsesApexSmoothSteering();
            
            // TACTICAL DIRECTION: Predator Retreat (User REQ: Flee strictly from threat)
            if (isRetreating && threatPos != default)
            {
                Vector3 retreatDirection = ResolveBodyRuntimePosition() - threatPos;
                if (retreatDirection.sqrMagnitude > MinDirectionSqr)
                    desiredDirection = ResolveSteeringDirection(retreatDirection, desiredDirection, useApexSteering);
            }

            Vector3 fallbackForward = _smoothedSteerDirection.sqrMagnitude > MinDirectionSqr
                ? ResolveSteeringDirection(_smoothedSteerDirection, Vector3.forward, useApexSteering)
                : (_body.rotation * Vector3.forward);
            if (fallbackForward.sqrMagnitude <= MinDirectionSqr)
                fallbackForward = Vector3.forward;

            Vector3 steeringTarget = desiredDirection.sqrMagnitude > MinDirectionSqr
                ? ResolveSteeringDirection(desiredDirection, fallbackForward, useApexSteering)
                : ResolveSteeringDirection(fallbackForward, Vector3.forward, useApexSteering);
            float steeringSharpness = math.max(0.01f, turnSpeed * math.max(0.1f, turnMult) * resolvedForceMultiplier);
            _smoothedSteerDirection = useApexSteering
                ? ResolveApexSteeringArc(fallbackForward, steeringTarget, fdt, steeringSharpness * ApexSteeringResponseScale)
                : steeringTarget;

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
                Vector3 desiredVelocityDirection = ResolveSteeringDirection(_smoothedSteerDirection, steeringTarget, useApexSteering);
                Vector3 desiredVelocity = desiredVelocityDirection * speedTarget;
                currentVelocity = MoveTowardsSteering(currentVelocity, desiredVelocity, maxVelocityDelta, useApexSteering);
            }
            else
            {
                currentVelocity = MoveTowardsSteering(currentVelocity, Vector3.zero, maxVelocityDelta, useApexSteering);
            }

            if (!IsFinite(currentVelocity))
                currentVelocity = Vector3.zero;

            IPhysicsService physicsService = _physicsService;
            if (physicsService != null)
                physicsService.QueueLinearVelocitySet(_body, currentVelocity);
            velocity = currentVelocity;
            currentSpeed = ResolveSpeedBucket(currentVelocity.sqrMagnitude);

            // 3. DIRECTION & ROTATION
            Vector3 facingDirection = currentVelocity.sqrMagnitude > 0.01f
                ? ResolveSteeringDirection(currentVelocity, currentDirection, useApexSteering)
                : currentDirection;
            if (facingDirection.sqrMagnitude > 0.01f)
            {
                Vector3 lookDir = ResolveSteeringDirection(facingDirection, Vector3.forward, useApexSteering);
                Quaternion targetRot = useApexSteering
                    ? ResolveApexRotation(lookDir, _body.rotation)
                    : ResolveDominantAxisRotation(lookDir);
                
                // Rotation Speed multiplier for aggressive/retreat states
                float rotMod = (isRetreating || speedMult > 1.1f) ? 2.5f * turnMult : turnMult;
                float turnResponse = turnSpeed * rotMod * resolvedForceMultiplier * fdt;
                turnResponse = useApexSteering
                    ? SmoothStep01(turnResponse * ApexSteeringResponseScale)
                    : math.saturate(turnResponse);
                Quaternion nextRotation = FastNlerp(_body.rotation, targetRot, turnResponse);

                // 4. VISUAL BANKING (User REQ: Fluid aquatic tilt)
                float lateralTurn = Vector3.Dot(nextRotation * Vector3.right, lookDir);
                float bankIntensity = _speciesProfile != null ? _speciesProfile.turnBankingIntensity : 1.0f;
                // Heavy banking for retreats (User REQ)
                float bankMult = isRetreating ? 2.0f : 1.0f;
                float targetRoll = -lateralTurn * bankingStrength * bankIntensity * bankMult;
                float bankResponse = useApexSteering ? ApexBankResponse : SwarmBankResponse;
                _lastBankingRoll = math.lerp(_lastBankingRoll, targetRoll, math.saturate(bankResponse * fdt));

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
            _smoothedSteerDirection = currentDirection.sqrMagnitude > MinDirectionSqr ? currentDirection : Vector3.forward;
        }

        private bool UsesApexSmoothSteering()
        {
            if (_speciesProfile == null || !_speciesProfile.isLeviathan)
                return false;

            return true;
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

        private Vector3 ResolveBodyRuntimePosition()
        {
            return _body != null ? _body.position : Vector3.zero;
        }

        private static Vector3 ResolveDominantAxisDirection(Vector3 direction, Vector3 fallback)
        {
            if (!IsFinite(direction) || direction.sqrMagnitude <= MinDirectionSqr)
                direction = fallback;

            if (!IsFinite(direction) || direction.sqrMagnitude <= MinDirectionSqr)
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

        private static Vector3 ResolveSteeringDirection(Vector3 direction, Vector3 fallback, bool useApexSteering)
        {
            return useApexSteering
                ? ResolveApexSmoothDirection(direction, fallback)
                : ResolveDominantAxisDirection(direction, fallback);
        }

        private static Vector3 ResolveApexSmoothDirection(Vector3 direction, Vector3 fallback)
        {
            if (!IsFinite(direction) || direction.sqrMagnitude <= MinDirectionSqr)
                direction = fallback;

            float lengthSq = direction.sqrMagnitude;
            if (!IsFinite(direction) || lengthSq <= MinDirectionSqr)
                return Vector3.forward;

            return direction * math.rsqrt(lengthSq);
        }

        private static float ResolveSpeedBucket(float velocitySqr)
        {
            if (velocitySqr >= 100f)
                return 10f;
            if (velocitySqr >= 25f)
                return 5f;
            if (velocitySqr >= 4f)
                return 2f;
            return velocitySqr > 0.0001f ? 1f : 0f;
        }

        private static Vector3 MoveTowardsAxis(Vector3 current, Vector3 target, float maxDelta)
        {
            Vector3 delta = target - current;
            float distanceSq = delta.sqrMagnitude;
            float maxDeltaSq = maxDelta * maxDelta;
            if (distanceSq <= maxDeltaSq || distanceSq <= MinDirectionSqr)
                return target;

            return current + ResolveDominantAxisDirection(delta, Vector3.zero) * maxDelta;
        }

        private static Vector3 MoveTowardsSteering(Vector3 current, Vector3 target, float maxDelta, bool useApexSteering)
        {
            if (!useApexSteering)
                return MoveTowardsAxis(current, target, maxDelta);

            Vector3 delta = target - current;
            float distanceSq = delta.sqrMagnitude;
            float maxDeltaSq = maxDelta * maxDelta;
            if (distanceSq <= maxDeltaSq || distanceSq <= MinDirectionSqr || !IsFinite(delta))
                return target;

            float step01 = math.saturate(maxDelta * math.rsqrt(distanceSq));
            return current + (delta * step01);
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

        private static Vector3 ResolveApexSteeringArc(Vector3 currentSteer, Vector3 desiredSteer, float deltaTime, float turnRate)
        {
            Vector3 currentSafe = ResolveApexSmoothDirection(currentSteer, Vector3.forward);
            Vector3 desiredSafe = ResolveApexSmoothDirection(desiredSteer, currentSafe);
            float x = math.max(0f, deltaTime) * math.max(0f, turnRate);
            float alpha = SmoothStep01(x * math.rcp(1f + x));
            quaternion currentRotation = quaternion.LookRotationSafe((float3)currentSafe, new float3(0f, 1f, 0f));
            quaternion desiredRotation = quaternion.LookRotationSafe((float3)desiredSafe, new float3(0f, 1f, 0f));
            quaternion smoothedRotation = CinematicMath.FastNlerp(currentRotation, desiredRotation, alpha);
            float3 forward = math.mul(smoothedRotation, new float3(0f, 0f, 1f));
            return ResolveApexSmoothDirection(new Vector3(forward.x, forward.y, forward.z), desiredSafe);
        }

        private static Quaternion ResolveApexRotation(Vector3 direction, Quaternion fallback)
        {
            Vector3 fallbackForward = fallback * Vector3.forward;
            Vector3 safeDirection = ResolveApexSmoothDirection(direction, fallbackForward);
            quaternion rotation = quaternion.LookRotationSafe((float3)safeDirection, new float3(0f, 1f, 0f));
            float4 value = rotation.value;
            if (!math.all(math.isfinite(value)))
                return fallback;

            return new Quaternion(value.x, value.y, value.z, value.w);
        }

        private static Quaternion FastNlerp(Quaternion from, Quaternion to, float t)
        {
            quaternion blended = CinematicMath.FastNlerp(
                new quaternion(from.x, from.y, from.z, from.w),
                new quaternion(to.x, to.y, to.z, to.w),
                t);
            float4 value = blended.value;
            return new Quaternion(value.x, value.y, value.z, value.w);
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
            if (lengthSq <= MinQuaternionLengthSq || !float.IsFinite(lengthSq))
                return _ForwardRotation;

            float invLength = math.rcp(math.max(0.0001f, 0.5f + (lengthSq * 0.5f)));
            value.x *= invLength;
            value.y *= invLength;
            value.z *= invLength;
            value.w *= invLength;
            return value;
        }

        private static float SmoothStep01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - (2f * x));
        }
    }
}
