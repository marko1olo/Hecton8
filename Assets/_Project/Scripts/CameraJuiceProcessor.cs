// ============================================================================
// HECTON-8 — CameraJuiceProcessor.cs  v7.0a
// Three-zone camera juice: Land / Surface Swim / Deep Swim
// Spring-damped roll, momentum pitch, turn sway, surface bob.
//
// v7.0a FIXES:
//   • Removed pitch inertia offset — was causing reverse-direction jerk
//     on vertical mouse movement. Body yaw lag is sufficient for mass feel.
//   • Exhale rhythm: removed pitch pulse (was jerky), softened dip impulse
//
// v7.0 FEATURES (all preserved):
//   • Swim bob, collision shake, splash dip, depth sway/roll multipliers
//   • FOV depth compression, exhale rhythm (dip only), submerge detection
// ============================================================================

using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public struct CameraJuiceOutput
    {
        public Vector3 localPositionOffset;
        public float rollOffset;
        public float pitchOffset;
        public float fovOffset;
        public bool stepEvent;
    }

    public struct CameraJuiceInput
    {
        public bool isWalking;
        public bool isGrounded;
        public bool hasMovementInput;
        public float inputH;
        public float mouseXDelta;
        public float horizontalSpeed;
        public float verticalVelocity;
        public bool wasGroundedLastFrame;
        public float deltaTime;
        public float immersionRatio;
        public float speedDelta;
        public float yawDelta;

        // v7.0
        public float depth;
        public float swimSpeed;
        public float cameraPitch;    // still passed in, but pitch inertia disabled
    }

    public sealed class CameraJuiceProcessor
    {
        // ── Head Bob ──
        private float _bobTimer;
        private float _bobIntensity;
        private bool _wasInLowPhase;

        // ── Swim Bob ──
        private float _swimBobTimer;
        private float _swimBobIntensity;

        // ── Idle Sway ──
        private float _swayTimer;
        private float _swayIntensity;

        // ── Surface Bob ──
        private float _surfaceBobTimer;

        // ── Landing Impact ──
        private float _impactDipCurrent;
        private float _impactDipVelocity;
        private float _preLandingVerticalVelocity;

        // ── Collision Shake ──
        private float _collisionShakeY;
        private float _collisionShakeYVel;
        private float _collisionShakeX;
        private float _collisionShakeXVel;
        private float _collisionShakePitch;
        private float _collisionShakePitchVel;

        // ── Splash Dip ──
        private float _splashDipCurrent;
        private float _splashDipVelocity;

        // ── Roll (spring) ──
        private float _currentRoll;
        private float _rollVelocity;
        private float _targetRoll;
        private float _rollSign;

        // ── Momentum Pitch ──
        private float _momentumPitch;
        private float _momentumPitchVelocity;

        // ── Turn Sway ──
        private float _turnSwayX;
        private float _turnSwayXVelocity;

        // ── Exhale Rhythm ──
        private float _exhaleTimer;
        private float _exhaleNextInterval;
        private float _exhalePhase;
        private float _exhaleDipCurrent;
        private float _exhaleDipVelocity;

        // ── Submerge State ──
        private bool _wasSubmerged;
        private float _prevImmersionRatio;
        private bool _submergeChangeThisFrame;
        private bool _submergedStateThisFrame;
        private bool _splashThisFrame;
        private float _splashIntensityThisFrame;
        private bool _exhaleThisFrame;

        // ── Output ──
        private CameraJuiceOutput _output;

        // ── Constants ──
        private const float TWO_PI = 6.2831853f;
        private const float BOB_STEP_PHASE_THRESHOLD = -0.9f;
        private const float DEAD_ZONE = 0.001f;
        private const float IMPACT_RECOVERY_OMEGA = 6f;
        private const float TURN_SWAY_OMEGA = 10f;
        private const float TURN_SWAY_MAX_OFFSET = 0.012f;
        private const float TURN_SWAY_SENSITIVITY = 0.002f;
        private const float SPLASH_DIP_RECOVERY_OMEGA = 8f;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC — EVENTS (polled, zero GC)
        // ══════════════════════════════════════════════════════════

        public bool SubmergeChangedThisFrame => _submergeChangeThisFrame;
        public bool IsSubmerged => _submergedStateThisFrame;
        public bool SplashThisFrame => _splashThisFrame;
        public float SplashIntensity => _splashIntensityThisFrame;
        public bool ExhaleThisFrame => _exhaleThisFrame;
        public float CurrentRoll => _currentRoll;

        public void Initialize(bool leanIntoTurn)
        {
            _bobTimer = 0f;
            _bobIntensity = 0f;
            _wasInLowPhase = false;
            _swimBobTimer = 0f;
            _swimBobIntensity = 0f;
            _swayTimer = 0f;
            _swayIntensity = 0f;
            _surfaceBobTimer = 0f;
            _impactDipCurrent = 0f;
            _impactDipVelocity = 0f;
            _preLandingVerticalVelocity = 0f;
            _collisionShakeY = 0f;
            _collisionShakeYVel = 0f;
            _collisionShakeX = 0f;
            _collisionShakeXVel = 0f;
            _collisionShakePitch = 0f;
            _collisionShakePitchVel = 0f;
            _splashDipCurrent = 0f;
            _splashDipVelocity = 0f;
            _currentRoll = 0f;
            _rollVelocity = 0f;
            _targetRoll = 0f;
            _momentumPitch = 0f;
            _momentumPitchVelocity = 0f;
            _turnSwayX = 0f;
            _turnSwayXVelocity = 0f;
            _exhaleTimer = 0f;
            _exhaleNextInterval = 4.5f;
            _exhalePhase = 0f;
            _exhaleDipCurrent = 0f;
            _exhaleDipVelocity = 0f;
            _wasSubmerged = false;
            _prevImmersionRatio = 0f;
            _submergeChangeThisFrame = false;
            _submergedStateThisFrame = false;
            _splashThisFrame = false;
            _splashIntensityThisFrame = 0f;
            _exhaleThisFrame = false;
            _rollSign = leanIntoTurn ? -1f : 1f;
            _output = default;
        }

        // ══════════════════════════════════════════════════════════
        //  EXTERNAL IMPULSE REGISTRATION
        // ══════════════════════════════════════════════════════════

        public void RegisterCollisionImpulse(float relativeSpeed, SuitData suit)
        {
            if (suit == null || !suit.enableCollisionShake) return;
            if (relativeSpeed < suit.collisionShakeThreshold) return;

            float norm = math.saturate(
                (relativeSpeed - suit.collisionShakeThreshold) /
                math.max(suit.collisionShakeMaxVelocity - suit.collisionShakeThreshold, 0.1f));

            float hash = math.frac(Time.time * 17.31f);
            float signX = hash > 0.5f ? 1f : -1f;
            float signP = math.frac(Time.time * 7.13f) > 0.5f ? 1f : -1f;

            _collisionShakeY = -norm * suit.collisionShakeMaxAmplitude;
            _collisionShakeYVel = norm * suit.collisionShakeMaxAmplitude * 3f;
            _collisionShakeX = signX * norm * suit.collisionShakeMaxAmplitude * 0.6f;
            _collisionShakeXVel = -signX * norm * suit.collisionShakeMaxAmplitude * 2f;
            _collisionShakePitch = signP * norm * suit.collisionShakeMaxPitch;
            _collisionShakePitchVel = -signP * norm * suit.collisionShakeMaxPitch * 2.5f;
        }

        public void RegisterSplash(float intensity, SuitData suit)
        {
            if (suit == null) return;
            float dip = -intensity * suit.splashCameraDip;
            _splashDipCurrent = dip;
            _splashDipVelocity = -dip * 2f;
        }

        // ══════════════════════════════════════════════════════════
        //  MAIN PROCESS
        // ══════════════════════════════════════════════════════════

        public CameraJuiceOutput Process(in CameraJuiceInput input, SuitData suit)
        {
            _output.localPositionOffset.x = 0f;
            _output.localPositionOffset.y = 0f;
            _output.localPositionOffset.z = 0f;
            _output.rollOffset = 0f;
            _output.pitchOffset = 0f;
            _output.fovOffset = 0f;
            _output.stepEvent = false;

            _submergeChangeThisFrame = false;
            _splashThisFrame = false;
            _splashIntensityThisFrame = 0f;
            _exhaleThisFrame = false;

            float dt = input.deltaTime;
            if (dt <= 0f) return _output;

            // ── Water events ──
            DetectWaterEvents(in input, suit, dt);

            // ── Collision shake recovery ──
            ProcessCollisionShake(suit, dt);

            // ── Splash dip recovery ──
            ProcessSplashDip(dt);

            // ── FOV compression ──
            ProcessDepthFovCompression(in input, suit);

            if (input.isWalking)
            {
                ProcessHeadBob(in input, suit, dt);
                ProcessLandingImpact(in input, suit, dt);
                DecaySwimEffects(dt, suit);
            }
            else
            {
                float deepFactor = math.saturate((input.immersionRatio - 0.75f) * 4f);
                float surfaceFactor = 1f - deepFactor;

                float depthSwayMul = ComputeDepthMultiplier(
                    input.depth, suit.depthSwayStart, suit.depthSwayEnd, suit.depthSwayMultiplierMax);
                float depthRollMul = ComputeDepthMultiplier(
                    input.depth, suit.depthSwayStart, suit.depthSwayEnd, suit.depthRollMultiplierMax);

                if (surfaceFactor > 0.01f)
                    ProcessSurfaceBob(in input, suit, dt, surfaceFactor);

                ProcessSwimBob(in input, suit, dt);

                if (deepFactor > 0.01f)
                    ProcessIdleSway(in input, suit, dt, deepFactor * depthSwayMul);

                float rollScale = (0.3f + 0.7f * deepFactor) * depthRollMul;
                ProcessSwimRoll(in input, suit, dt, rollScale);

                if (deepFactor > 0.1f)
                    ProcessMomentumPitch(in input, suit, dt, deepFactor);

                float turnScale = 0.5f + 0.5f * deepFactor;
                ProcessTurnSway(in input, dt, turnScale);

                ProcessExhaleRhythm(in input, suit, dt);

                DecayWalkEffects(dt, suit);
            }

            // ── Accumulate offsets ──
            _output.localPositionOffset.y += _impactDipCurrent;
            _output.localPositionOffset.y += _splashDipCurrent;
            _output.localPositionOffset.y += _collisionShakeY;
            _output.localPositionOffset.x += _collisionShakeX;
            _output.pitchOffset += _collisionShakePitch;
            _output.localPositionOffset.y += _exhaleDipCurrent;

            _prevImmersionRatio = input.immersionRatio;

            return _output;
        }

        public void TrackVerticalVelocity(float v) { _preLandingVerticalVelocity = v; }

        // ══════════════════════════════════════════════════════════
        //  DEPTH MULTIPLIER
        // ══════════════════════════════════════════════════════════

        private static float ComputeDepthMultiplier(float depth, float start, float end, float maxMul)
        {
            if (depth <= start) return 1f;
            if (depth >= end) return maxMul;
            float t = (depth - start) / math.max(end - start, 0.01f);
            return 1f + (maxMul - 1f) * t;
        }

        // ══════════════════════════════════════════════════════════
        //  WATER EVENT DETECTION
        // ══════════════════════════════════════════════════════════

        private void DetectWaterEvents(in CameraJuiceInput input, SuitData suit, float dt)
        {
            float threshold = suit != null ? suit.submergeThreshold : 0.85f;
            bool submergedNow = input.immersionRatio >= threshold;

            if (submergedNow != _wasSubmerged)
            {
                _submergeChangeThisFrame = true;
                _submergedStateThisFrame = submergedNow;
            }
            else
            {
                _submergedStateThisFrame = submergedNow;
            }
            _wasSubmerged = submergedNow;

            if (suit != null && dt > 0f)
            {
                float immersionRate = math.abs(input.immersionRatio - _prevImmersionRatio) / dt;

                if (immersionRate >= suit.splashImmersionRateThreshold)
                {
                    float verticalFactor = math.saturate(
                        math.abs(input.verticalVelocity) / math.max(suit.splashMinVerticalSpeed * 3f, 1f));
                    float rateFactor = math.saturate(
                        immersionRate / (suit.splashImmersionRateThreshold * 3f));

                    float intensity = math.max(verticalFactor, rateFactor);
                    intensity = math.saturate(intensity);

                    if (intensity > 0.1f)
                    {
                        _splashThisFrame = true;
                        _splashIntensityThisFrame = intensity;
                        RegisterSplash(intensity, suit);
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  COLLISION SHAKE
        // ══════════════════════════════════════════════════════════

        private void ProcessCollisionShake(SuitData suit, float dt)
        {
            float omega = suit != null ? suit.collisionShakeRecoveryOmega : 10f;

            if (math.abs(_collisionShakeY) > 0.0001f || math.abs(_collisionShakeYVel) > 0.0001f)
            {
                _collisionShakeY = SpringDamp(_collisionShakeY, 0f,
                    ref _collisionShakeYVel, omega, dt);
            }
            else { _collisionShakeY = 0f; _collisionShakeYVel = 0f; }

            if (math.abs(_collisionShakeX) > 0.0001f || math.abs(_collisionShakeXVel) > 0.0001f)
            {
                _collisionShakeX = SpringDamp(_collisionShakeX, 0f,
                    ref _collisionShakeXVel, omega, dt);
            }
            else { _collisionShakeX = 0f; _collisionShakeXVel = 0f; }

            if (math.abs(_collisionShakePitch) > 0.001f || math.abs(_collisionShakePitchVel) > 0.001f)
            {
                _collisionShakePitch = SpringDamp(_collisionShakePitch, 0f,
                    ref _collisionShakePitchVel, omega, dt);
            }
            else { _collisionShakePitch = 0f; _collisionShakePitchVel = 0f; }
        }

        // ══════════════════════════════════════════════════════════
        //  SPLASH DIP
        // ══════════════════════════════════════════════════════════

        private void ProcessSplashDip(float dt)
        {
            if (math.abs(_splashDipCurrent) > 0.0001f || math.abs(_splashDipVelocity) > 0.0001f)
            {
                _splashDipCurrent = SpringDamp(_splashDipCurrent, 0f,
                    ref _splashDipVelocity, SPLASH_DIP_RECOVERY_OMEGA, dt);
            }
            else { _splashDipCurrent = 0f; _splashDipVelocity = 0f; }
        }

        // ══════════════════════════════════════════════════════════
        //  FOV DEPTH COMPRESSION
        // ══════════════════════════════════════════════════════════

        private void ProcessDepthFovCompression(in CameraJuiceInput input, SuitData suit)
        {
            if (suit == null || !suit.enableDepthFovCompression) return;
            if (input.depth <= suit.depthFovCompressionStart) return;

            float t = math.saturate(
                (input.depth - suit.depthFovCompressionStart) /
                math.max(suit.depthFovCompressionEnd - suit.depthFovCompressionStart, 0.01f));
            t = t * t * (3f - 2f * t);

            _output.fovOffset = -t * suit.depthFovCompressionMax;
        }

        // ══════════════════════════════════════════════════════════
        //  SWIM BOB
        // ══════════════════════════════════════════════════════════

        private void ProcessSwimBob(in CameraJuiceInput input, SuitData suit, float dt)
        {
            if (suit == null || !suit.enableSwimBob) return;

            float targetIntensity = input.hasMovementInput ? 1f : 0f;
            float blendT = 1f - math.exp(-suit.swimBobTransitionSpeed * dt);
            _swimBobIntensity = math.lerp(_swimBobIntensity, targetIntensity, blendT);

            if (_swimBobIntensity < DEAD_ZONE) { _swimBobIntensity = 0f; return; }

            float maxSwim = suit.maxSwimSpeed > 0f ? suit.maxSwimSpeed : 9f;
            float speedFactor = math.saturate(input.swimSpeed / maxSwim);
            speedFactor = math.max(speedFactor, 0.3f);

            _swimBobTimer += suit.swimBobFrequency * TWO_PI * dt;
            if (_swimBobTimer > 100000f) _swimBobTimer -= 100000f;

            float scale = _swimBobIntensity * speedFactor;

            float verticalBob = math.sin(_swimBobTimer) * suit.swimBobVerticalAmplitude * scale;
            float forwardBob = math.sin(_swimBobTimer * 0.5f) * suit.swimBobForwardAmplitude * scale;
            float rollBob = math.sin(_swimBobTimer * 0.5f + 1.57f) * suit.swimBobRollAmplitude * scale;

            _output.localPositionOffset.y += verticalBob;
            _output.localPositionOffset.z += forwardBob;
            _output.rollOffset += rollBob;
        }

        // ══════════════════════════════════════════════════════════
        //  EXHALE RHYTHM — dip only, no pitch (v7.0a fix)
        // ══════════════════════════════════════════════════════════

        private void ProcessExhaleRhythm(in CameraJuiceInput input, SuitData suit, float dt)
        {
            if (suit == null || !suit.enableExhaleRhythm) return;

            if (input.immersionRatio < 0.7f)
            {
                _exhaleTimer = 0f;
                _exhalePhase = 0f;
                RecoverExhaleSpring(dt);
                return;
            }

            _exhaleTimer += dt;

            if (_exhaleTimer >= _exhaleNextInterval && _exhalePhase <= 0f)
            {
                _exhalePhase = suit.exhaleDuration;
                _exhaleTimer = 0f;
                _exhaleThisFrame = true;

                float variation = suit.exhaleIntervalVariation;
                float hash = math.frac(Time.time * 3.17f) * 2f - 1f;
                _exhaleNextInterval = suit.exhaleIntervalBase + hash * variation;
                _exhaleNextInterval = math.max(_exhaleNextInterval, 1.5f);

                // v7.0a: gentler impulse, no pitch component
                _exhaleDipCurrent = -suit.exhaleDipAmplitude;
                _exhaleDipVelocity = suit.exhaleDipAmplitude * 0.8f;
            }

            if (_exhalePhase > 0f)
            {
                _exhalePhase -= dt;
                if (_exhalePhase < 0f) _exhalePhase = 0f;
            }

            RecoverExhaleSpring(dt);
        }

        private void RecoverExhaleSpring(float dt)
        {
            if (math.abs(_exhaleDipCurrent) > 0.0001f || math.abs(_exhaleDipVelocity) > 0.0001f)
            {
                _exhaleDipCurrent = SpringDamp(_exhaleDipCurrent, 0f,
                    ref _exhaleDipVelocity, 6f, dt);
            }
            else { _exhaleDipCurrent = 0f; _exhaleDipVelocity = 0f; }
        }

        // ══════════════════════════════════════════════════════════
        //  HEAD BOB
        // ══════════════════════════════════════════════════════════

        private void ProcessHeadBob(in CameraJuiceInput input, SuitData suit, float dt)
        {
            float targetIntensity = (input.isGrounded && input.hasMovementInput) ? 1f : 0f;
            float blendT = 1f - math.exp(-suit.bobTransitionSpeed * dt);
            _bobIntensity = math.lerp(_bobIntensity, targetIntensity, blendT);

            if (_bobIntensity < DEAD_ZONE) { _bobIntensity = 0f; _wasInLowPhase = false; return; }

            float speedFactor = suit.maxWalkSpeed > 0f
                ? math.clamp(input.horizontalSpeed / suit.maxWalkSpeed, 0f, 1f) : 0f;

            _bobTimer += suit.bobFrequency * TWO_PI * dt * speedFactor;
            if (_bobTimer > 100000f) _bobTimer -= 100000f;

            float sinVal = math.sin(_bobTimer);
            float cosVal = math.cos(_bobTimer * 0.5f);

            _output.localPositionOffset.x += cosVal * suit.bobHorizontalAmplitude * _bobIntensity;
            _output.localPositionOffset.y += sinVal * suit.bobVerticalAmplitude * _bobIntensity;

            bool inLowPhase = sinVal < BOB_STEP_PHASE_THRESHOLD;
            if (inLowPhase && !_wasInLowPhase && _bobIntensity > 0.5f)
                _output.stepEvent = true;
            _wasInLowPhase = inLowPhase;
        }

        // ══════════════════════════════════════════════════════════
        //  LANDING IMPACT
        // ══════════════════════════════════════════════════════════

        private void ProcessLandingImpact(in CameraJuiceInput input, SuitData suit, float dt)
        {
            if (input.isGrounded && !input.wasGroundedLastFrame)
            {
                float fallSpeed = math.abs(_preLandingVerticalVelocity);
                if (fallSpeed >= suit.impactVelocityThreshold)
                {
                    float norm = math.clamp(
                        (fallSpeed - suit.impactVelocityThreshold) /
                        math.max(suit.impactVelocityMax - suit.impactVelocityThreshold, 0.1f),
                        0f, 1f);
                    _impactDipCurrent = -norm * suit.impactDipMaxDistance;
                    _impactDipVelocity = norm * suit.impactDipMaxDistance * 2f;
                }
            }

            if (math.abs(_impactDipCurrent) > 0.0001f || math.abs(_impactDipVelocity) > 0.0001f)
            {
                _impactDipCurrent = SpringDamp(_impactDipCurrent, 0f,
                    ref _impactDipVelocity, suit.impactRecoverySpeed, dt);
            }
            else { _impactDipCurrent = 0f; _impactDipVelocity = 0f; }
        }

        // ══════════════════════════════════════════════════════════
        //  SURFACE BOB
        // ══════════════════════════════════════════════════════════

        private void ProcessSurfaceBob(in CameraJuiceInput input, SuitData suit, float dt, float intensity)
        {
            _surfaceBobTimer += dt;
            if (_surfaceBobTimer > 100000f) _surfaceBobTimer -= 100000f;

            float wave1 = math.sin(_surfaceBobTimer * 0.7f) * 0.05f;
            float wave2 = math.sin(_surfaceBobTimer * 1.1f + 2.1f) * 0.025f;
            float wave3 = math.sin(_surfaceBobTimer * 0.3f + 4.7f) * 0.015f;
            float waveRoll = math.sin(_surfaceBobTimer * 0.4f + 0.7f) * 1.5f;
            float wavePitch = math.sin(_surfaceBobTimer * 0.55f + 1.3f) * 0.6f;

            float finalIntensity = intensity;

            if (input.hasMovementInput)
                finalIntensity *= 0.4f;

            _output.localPositionOffset.y += (wave1 + wave2 + wave3) * finalIntensity;
            _output.rollOffset += waveRoll * finalIntensity;
            _output.pitchOffset += wavePitch * finalIntensity;
        }

        // ══════════════════════════════════════════════════════════
        //  IDLE SWAY
        // ══════════════════════════════════════════════════════════

        private void ProcessIdleSway(in CameraJuiceInput input, SuitData suit, float dt, float depthScale)
        {
            if (!suit.enableIdleSway) return;

            float targetSwayIntensity = input.hasMovementInput ? 0f : 1f;
            float swayBlendT = 1f - math.exp(-suit.idleSwayTransitionSpeed * dt);
            _swayIntensity = math.lerp(_swayIntensity, targetSwayIntensity, swayBlendT);

            if (_swayIntensity < DEAD_ZONE) { _swayIntensity = 0f; return; }

            _swayTimer += dt;
            if (_swayTimer > 100000f) _swayTimer -= 100000f;

            float scale = _swayIntensity * depthScale;

            float swayY = math.sin(_swayTimer * suit.idleSwayFrequencyY * TWO_PI)
                        * suit.idleSwayAmplitudeY * scale;
            float swayX = math.sin(_swayTimer * suit.idleSwayFrequencyX * TWO_PI)
                        * suit.idleSwayAmplitudeX * scale;
            float swayRoll = math.sin(_swayTimer * suit.idleSwayFrequencyRoll * TWO_PI)
                           * suit.idleSwayAmplitudeRoll * scale;

            _output.localPositionOffset.x += swayX;
            _output.localPositionOffset.y += swayY;
            _output.rollOffset += swayRoll;
        }

        // ══════════════════════════════════════════════════════════
        //  SWIM ROLL
        // ══════════════════════════════════════════════════════════

        private void ProcessSwimRoll(in CameraJuiceInput input, SuitData suit, float dt, float rollScale)
        {
            float strafeContrib = input.inputH * suit.strafeRollAngle * _rollSign;

            float mouseNormalized = math.clamp(
                input.mouseXDelta * suit.mouseRollSensitivity, -1f, 1f);
            float mouseContrib = mouseNormalized * suit.mouseRollAngle * _rollSign;

            float maxTotal = suit.strafeRollAngle + suit.mouseRollAngle;
            _targetRoll = math.clamp(strafeContrib + mouseContrib, -maxTotal, maxTotal);
            _targetRoll *= rollScale;

            _currentRoll = SpringDampNoOvershoot(_currentRoll, _targetRoll,
                ref _rollVelocity, suit.rollSpringOmega, dt);

            _output.rollOffset += _currentRoll;
        }

        // ══════════════════════════════════════════════════════════
        //  MOMENTUM PITCH
        // ══════════════════════════════════════════════════════════

        private void ProcessMomentumPitch(in CameraJuiceInput input, SuitData suit, float dt, float depthScale)
        {
            if (suit.accelerationPitchFactor <= 0f) return;

            float targetPitch = -input.speedDelta * suit.accelerationPitchFactor * depthScale;
            targetPitch = math.clamp(targetPitch, -2f, 2f);

            _momentumPitch = SpringDamp(_momentumPitch, targetPitch,
                ref _momentumPitchVelocity, suit.momentumPitchSpringOmega, dt);

            _output.pitchOffset += _momentumPitch;
        }

        // ══════════════════════════════════════════════════════════
        //  TURN SWAY
        // ══════════════════════════════════════════════════════════

        private void ProcessTurnSway(in CameraJuiceInput input, float dt, float scale)
        {
            float targetX = -input.yawDelta * TURN_SWAY_SENSITIVITY * scale;
            targetX = math.clamp(targetX, -TURN_SWAY_MAX_OFFSET, TURN_SWAY_MAX_OFFSET);

            _turnSwayX = SpringDampNoOvershoot(_turnSwayX, targetX,
                ref _turnSwayXVelocity, TURN_SWAY_OMEGA, dt);

            _output.localPositionOffset.x += _turnSwayX;
        }

        // ══════════════════════════════════════════════════════════
        //  CROSS-MODE DECAY
        // ══════════════════════════════════════════════════════════

        private void DecaySwimEffects(float dt, SuitData suit)
        {
            _targetRoll = 0f;
            if (math.abs(_currentRoll) > 0.01f || math.abs(_rollVelocity) > 0.01f)
            {
                _currentRoll = SpringDampNoOvershoot(_currentRoll, 0f, ref _rollVelocity, suit.rollSpringOmega, dt);
                _output.rollOffset += _currentRoll;
            }
            else { _currentRoll = 0f; _rollVelocity = 0f; }

            if (_swayIntensity > DEAD_ZONE)
            {
                float swayT = 1f - math.exp(-suit.idleSwayTransitionSpeed * dt);
                _swayIntensity = math.lerp(_swayIntensity, 0f, swayT);
            }
            else { _swayIntensity = 0f; }

            if (math.abs(_momentumPitch) > 0.001f || math.abs(_momentumPitchVelocity) > 0.001f)
            {
                _momentumPitch = SpringDamp(_momentumPitch, 0f, ref _momentumPitchVelocity, 6f, dt);
                _output.pitchOffset += _momentumPitch;
            }
            else { _momentumPitch = 0f; _momentumPitchVelocity = 0f; }

            if (math.abs(_turnSwayX) > 0.0001f || math.abs(_turnSwayXVelocity) > 0.0001f)
            {
                _turnSwayX = SpringDampNoOvershoot(_turnSwayX, 0f, ref _turnSwayXVelocity, TURN_SWAY_OMEGA, dt);
                _output.localPositionOffset.x += _turnSwayX;
            }
            else { _turnSwayX = 0f; _turnSwayXVelocity = 0f; }

            if (_swimBobIntensity > DEAD_ZONE)
            {
                float swimBobT = 1f - math.exp(-(suit != null ? suit.swimBobTransitionSpeed : 4f) * dt);
                _swimBobIntensity = math.lerp(_swimBobIntensity, 0f, swimBobT);
            }
            else { _swimBobIntensity = 0f; }

            RecoverExhaleSpring(dt);
        }

        private void DecayWalkEffects(float dt, SuitData suit)
        {
            if (_bobIntensity > DEAD_ZONE)
            {
                float bobT = 1f - math.exp(-suit.bobTransitionSpeed * dt);
                _bobIntensity = math.lerp(_bobIntensity, 0f, bobT);
                if (_bobIntensity > DEAD_ZONE)
                {
                    float sinVal = math.sin(_bobTimer);
                    float cosVal = math.cos(_bobTimer * 0.5f);
                    _output.localPositionOffset.y += sinVal * suit.bobVerticalAmplitude * _bobIntensity;
                    _output.localPositionOffset.x += cosVal * suit.bobHorizontalAmplitude * _bobIntensity;
                }
            }
            else { _bobIntensity = 0f; }

            if (math.abs(_impactDipCurrent) > 0.0001f || math.abs(_impactDipVelocity) > 0.0001f)
            {
                _impactDipCurrent = SpringDamp(_impactDipCurrent, 0f,
                    ref _impactDipVelocity, IMPACT_RECOVERY_OMEGA, dt);
            }
            else { _impactDipCurrent = 0f; _impactDipVelocity = 0f; }
        }

        // ══════════════════════════════════════════════════════════
        //  SPRING
        // ══════════════════════════════════════════════════════════

        private static float SpringDamp(float current, float target, ref float velocity, float omega, float dt)
        {
            float n1 = velocity - (current - target) * (omega * omega * dt);
            float n2 = 1f + omega * dt;
            velocity = n1 / (n2 * n2);
            return current + velocity * dt;
        }

        private static float SpringDampNoOvershoot(float current, float target, ref float velocity, float omega, float dt)
        {
            float next = SpringDamp(current, target, ref velocity, omega, dt);
            float prevDelta = current - target;
            float nextDelta = next - target;

            if ((prevDelta > 0f && nextDelta < 0f) || (prevDelta < 0f && nextDelta > 0f))
            {
                velocity = 0f;
                return target;
            }

            return next;
        }
    }
}
