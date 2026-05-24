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
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CameraJuiceOutput
    {
        public Vector3 localPositionOffset;
        public float rollOffset;
        public float pitchOffset;
        public float fovOffset;
        public byte stepEvent;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct CameraJuiceInput
    {
        public byte isWalking;
        public PlayerLocomotionMode locomotionMode;
        public byte isGrounded;
        public byte hasMovementInput;
        public float inputH;
        public float mouseXDelta;
        public float horizontalSpeed;
        public float verticalVelocity;
        public byte wasGroundedLastFrame;
        public float deltaTime;
        public float immersionRatio;
        public float speedDelta;
        public float yawDelta;

        // v7.0
        public float depth;
        public float swimSpeed;
        public float cameraPitch;    // still passed in, but pitch inertia disabled
        public PlayerSwimPresentationMode swimPresentationMode;
        public float swimStrokePhase;
        public float swimPropulsionPulse;
        public float swimStrokeImpulse;
        public float swimGuideWeight;
        public float swimVerticalInput;
        public float heavyCarryLoad;
        public float transportBoost01;
        public float transportCameraMotionScale;
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
        private float _externalRollImpulse;
        private float _externalRollImpulseVel;

        // ── Splash Dip ──
        private float _splashDipCurrent;
        private float _splashDipVelocity;
        private float _waterEntryFovTimer;
        private float _waterEntryFovDuration;
        private float _waterEntryFovExpandDegrees;
        private float _waterEntryFovCompressDegrees;
        private float _speedFovOffset;
        private float _abyssalNoirPulsePhase;

        // ── Action Bob (eating, healing) ──
        private float _actionBobY;
        private float _actionBobYVel;
        private float _actionBobX;
        private float _actionBobXVel;
        private float _actionBobIntensity;

        // ── Roll (spring) ──
        private float _currentRoll;
        private float _rollVelocity;
        private float _targetRoll;
        private float _rollSign;
        private float _cinematicShakeSign;

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
        private const float INV_TWO_PI = 0.15915494309189535f;
        private const float HALF_PI = 1.5707964f;
        private const float BOB_STEP_PHASE_THRESHOLD = -0.9f;
        private const float DEAD_ZONE = 0.001f;
        private const float IMPACT_RECOVERY_OMEGA = 6f;
        private const float TURN_SWAY_OMEGA = 10f;
        private const float TURN_SWAY_MAX_OFFSET = 0.012f;
        private const float TURN_SWAY_SENSITIVITY = 0.002f;
        private const float SPLASH_DIP_RECOVERY_OMEGA = 8f;
        private const float SWIM_PRESENTATION_VERTICAL_ASCEND_OFFSET = 0.0045f;
        private const float SWIM_PRESENTATION_VERTICAL_DESCEND_OFFSET = 0.0035f;
        private const float SWIM_PRESENTATION_ASCEND_PITCH = 0.28f;
        private const float SWIM_PRESENTATION_DESCEND_PITCH = 0.4f;
        private const float SWIM_PRESENTATION_PROPULSION_POSITION_KICK = 0.0055f;
        private const float SWIM_PRESENTATION_PROPULSION_FOV_KICK = 0.32f;
        private const float SWIM_PRESENTATION_PROPULSION_IMPULSE_POSITION_KICK = 0.0075f;
        private const float SWIM_PRESENTATION_PROPULSION_IMPULSE_FOV_KICK = 0.42f;
        private const float SWIM_PRESENTATION_MAX_POSITION_KICK = 0.0085f;
        private const float SWIM_PRESENTATION_MAX_FOV_KICK = 0.46f;
        private const float HEAVY_CARRY_MAX_HEADBOB_AMPLITUDE_SCALE = 1.16f;
        private const float HEAVY_CARRY_MAX_HEADBOB_CADENCE_SCALE = 0.76f;
        private const float HEAVY_CARRY_MAX_LANDING_DIP_SCALE = 1.12f;
        private const float HEAVY_CARRY_MAX_SURFACE_BOB_SCALE = 0.78f;
        private const float HEAVY_CARRY_MAX_TURN_SWAY_SCALE = 1.18f;
        private const float TRANSPORT_PROPULSION_FLOOR = 0.62f;
        private const float TRANSPORT_POSITION_KICK = 0.0065f;
        private const float TRANSPORT_FOV_KICK = 0.55f;
        private const float SPEED_FOV_START_METERS_PER_SECOND = 10f;
        private const float SPEED_FOV_FULL_METERS_PER_SECOND = 22f;
        private const float SPEED_FOV_MAX_DEGREES = 3.2f;
        private const float SPEED_FOV_LERP_SHARPNESS = 4.2f;
        private const float ABYSSAL_NOIR_PULSE_START_DEPTH_METERS = 38f;
        private const float ABYSSAL_NOIR_PULSE_FULL_DEPTH_METERS = 180f;
        private const float ABYSSAL_NOIR_PULSE_MAX_FOV_COMPRESS = 0.42f;
        private const float ABYSSAL_NOIR_PULSE_ROLL_DEGREES = 0.065f;
        private const float ABYSSAL_NOIR_PULSE_PITCH_DEGREES = 0.035f;

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
            _externalRollImpulse = 0f;
            _externalRollImpulseVel = 0f;
            _splashDipCurrent = 0f;
            _splashDipVelocity = 0f;
            _waterEntryFovTimer = 0f;
            _waterEntryFovDuration = 0f;
            _waterEntryFovExpandDegrees = 0f;
            _waterEntryFovCompressDegrees = 0f;
            _speedFovOffset = 0f;
            _abyssalNoirPulsePhase = 0f;
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
            _cinematicShakeSign = 1f;
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

            float signX = NextCinematicShakeSign();
            float signP = -signX;

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

        public void RegisterEntanglementStrain(float intensity)
        {
            intensity = math.saturate(intensity);
            if (intensity <= 0.0001f)
                return;

            float signX = NextCinematicShakeSign();
            float signP = -signX;
            float amplitude = intensity * 0.0035f;

            _collisionShakeY = math.min(_collisionShakeY, -amplitude);
            _collisionShakeYVel = math.max(_collisionShakeYVel, amplitude * 24f);
            _collisionShakeX = signX * amplitude * 0.8f;
            _collisionShakeXVel = -signX * amplitude * 18f;
            _collisionShakePitch = signP * intensity * 0.18f;
            _collisionShakePitchVel = -signP * intensity * 2.4f;
        }

        /// <summary>
        /// Registers a low-frequency active-sonar thump so the pulse reads as a suit/helmet body hit.
        /// </summary>
        public void RegisterSonarPingImpulse(float intensity)
        {
            intensity = math.saturate(intensity);
            if (intensity <= 0.0001f)
                return;

            float amplitude = intensity * 0.0042f;
            _collisionShakeY = math.min(_collisionShakeY, -amplitude);
            _collisionShakeYVel = math.max(_collisionShakeYVel, amplitude * 16f);
            _collisionShakePitch = math.min(_collisionShakePitch, -intensity * 0.12f);
            _collisionShakePitchVel = math.max(_collisionShakePitchVel, intensity * 0.95f);
        }

        /// <summary>
        /// Registers a signed roll impulse for emergency disorientation without allocating a coroutine/state wrapper.
        /// </summary>
        public void RegisterExternalRollImpulse(float signedDegrees)
        {
            float clampedDegrees = math.clamp(signedDegrees, -18f, 18f);
            if (math.abs(clampedDegrees) <= 0.001f)
                return;

            _externalRollImpulse = clampedDegrees;
            _externalRollImpulseVel = -clampedDegrees * 3.6f;
        }

        /// <summary>
        /// Registers a short FOV impulse for hard air-to-water transitions.
        /// </summary>
        /// <param name="expandDegrees">Initial positive FOV expansion in degrees.</param>
        /// <param name="compressDegrees">Follow-up negative FOV compression in degrees.</param>
        /// <param name="duration">Total impulse duration in seconds.</param>
        public void RegisterWaterEntryFovImpulse(float expandDegrees, float compressDegrees, float duration)
        {
            if (duration <= 0f)
                return;

            expandDegrees = math.max(0f, expandDegrees);
            compressDegrees = math.max(0f, compressDegrees);
            if (expandDegrees <= 0f && compressDegrees <= 0f)
                return;

            if (_waterEntryFovTimer <= 0f || expandDegrees >= _waterEntryFovExpandDegrees)
            {
                _waterEntryFovExpandDegrees = expandDegrees;
                _waterEntryFovCompressDegrees = compressDegrees;
                _waterEntryFovDuration = duration;
                _waterEntryFovTimer = duration;
                return;
            }

            if (expandDegrees > _waterEntryFovExpandDegrees)
                _waterEntryFovExpandDegrees = expandDegrees;

            if (compressDegrees > _waterEntryFovCompressDegrees)
                _waterEntryFovCompressDegrees = compressDegrees;

            if (duration > _waterEntryFovTimer)
            {
                _waterEntryFovDuration = duration;
                _waterEntryFovTimer = duration;
            }
        }

        /// <summary>
        /// Registers an action camera bob for eating/healing animations.
        /// Called by PlayerActionController during consumption actions.
        /// </summary>
        /// <param name="intensity">Bob intensity (0-1).</param>
        /// <param name="frequency">Bob frequency hint (affects randomness).</param>
        public void RegisterActionBob(float intensity, float frequency = 1f)
        {
            if (intensity <= 0f) return;

            _actionBobIntensity = intensity;

            float signX = NextCinematicShakeSign();

            _actionBobY = intensity * 0.008f;
            _actionBobYVel = intensity * 0.015f;
            _actionBobX = signX * intensity * 0.004f;
            _actionBobXVel = -signX * intensity * 0.008f;
        }

        /// <summary>
        /// Clears the action bob when action completes or is cancelled.
        /// </summary>
        public void ClearActionBob()
        {
            _actionBobIntensity = 0f;
        }

        internal void RegisterLandJumpLaunch()
        {
            _bobIntensity = 0f;
            _wasInLowPhase = false;
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
            _output.stepEvent = 0;

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
            ProcessWaterEntryFovImpulse(dt);
            ProcessSpeedFovCheat(in input, dt);
            ProcessAbyssalNoirPulse(in input, dt);

            float heavyCarryAmplitudeScale = math.lerp(1f, HEAVY_CARRY_MAX_HEADBOB_AMPLITUDE_SCALE, input.heavyCarryLoad);
            float heavyCarryCadenceScale = math.lerp(1f, HEAVY_CARRY_MAX_HEADBOB_CADENCE_SCALE, input.heavyCarryLoad);
            float heavyCarryLandingScale = math.lerp(1f, HEAVY_CARRY_MAX_LANDING_DIP_SCALE, input.heavyCarryLoad);
            float heavyCarrySurfaceScale = math.lerp(1f, HEAVY_CARRY_MAX_SURFACE_BOB_SCALE, input.heavyCarryLoad);
            float heavyCarryTurnScale = math.lerp(1f, HEAVY_CARRY_MAX_TURN_SWAY_SCALE, input.heavyCarryLoad);
            float transportTurnScale = math.lerp(1f, 0.9f, input.transportBoost01);

            switch (input.locomotionMode)
            {
                case PlayerLocomotionMode.DryGroundWalk:
                    ProcessHeadBob(in input, suit, dt, heavyCarryAmplitudeScale, heavyCarryCadenceScale);
                    ProcessLandingImpact(in input, suit, dt, heavyCarryLandingScale);
                    DecaySwimEffects(dt, suit);
                    break;

                case PlayerLocomotionMode.DryInteriorWalk:
                    ProcessHeadBob(in input, suit, dt, 0.82f * heavyCarryAmplitudeScale, 0.9f * heavyCarryCadenceScale);
                    ProcessLandingImpact(in input, suit, dt, 0.75f * heavyCarryLandingScale);
                    DecaySwimEffects(dt, suit);
                    break;

                case PlayerLocomotionMode.ShallowWadeWalk:
                    ProcessHeadBob(in input, suit, dt, 0.62f * heavyCarryAmplitudeScale, 0.78f * heavyCarryCadenceScale);
                    ProcessLandingImpact(in input, suit, dt, 0.7f * heavyCarryLandingScale);
                    ProcessSurfaceBob(in input, suit, dt, math.saturate(input.immersionRatio) * 0.18f * heavyCarrySurfaceScale);
                    DecaySwimEffects(dt, suit);
                    break;

                case PlayerLocomotionMode.ExosuitLocomotion:
                    ProcessHeadBob(in input, suit, dt, 0.46f * heavyCarryAmplitudeScale, 0.58f * heavyCarryCadenceScale);
                    ProcessLandingImpact(in input, suit, dt, 1.45f * heavyCarryLandingScale);
                    DecaySwimEffects(dt, suit);
                    break;

                case PlayerLocomotionMode.SurfaceSwim:
                    ProcessSurfaceBob(in input, suit, dt, heavyCarrySurfaceScale);
                    ProcessSwimBob(in input, suit, dt);
                    ProcessSwimRoll(in input, suit, dt, 0.3f);
                    ProcessTurnSway(in input, dt, 0.5f * heavyCarryTurnScale * transportTurnScale);
                    DecayWalkEffects(dt, suit);
                    break;

                default:
                    float deepFactor = math.max(
                        math.saturate((input.immersionRatio - 0.75f) * 4f),
                        0.2f);

                    float depthSwayMul = ComputeDepthMultiplier(
                        input.depth, suit.depthSwayStart, suit.depthSwayEnd, suit.depthSwayMultiplierMax);
                    float depthRollMul = ComputeDepthMultiplier(
                        input.depth, suit.depthSwayStart, suit.depthSwayEnd, suit.depthRollMultiplierMax);

                    ProcessSwimBob(in input, suit, dt);
                    ProcessIdleSway(in input, suit, dt, deepFactor * depthSwayMul);

                    float rollScale = (0.3f + 0.7f * deepFactor) * depthRollMul;
                    ProcessSwimRoll(in input, suit, dt, rollScale);
                    ProcessMomentumPitch(in input, suit, dt, deepFactor);

                    float turnScale = (0.5f + 0.5f * deepFactor) * heavyCarryTurnScale * transportTurnScale;
                    ProcessTurnSway(in input, dt, turnScale);
                    ProcessExhaleRhythm(in input, suit, dt);

                    DecayWalkEffects(dt, suit);
                    break;
            }

            // ── Accumulate offsets ──
            _output.localPositionOffset.y += _impactDipCurrent;
            _output.localPositionOffset.y += _splashDipCurrent;
            _output.localPositionOffset.y += _collisionShakeY;
            _output.localPositionOffset.x += _collisionShakeX;
            _output.pitchOffset += _collisionShakePitch;
            _output.rollOffset += _externalRollImpulse;
            _output.localPositionOffset.y += _exhaleDipCurrent;

            // ── Action bob (eating, healing) ──
            ProcessActionBob(dt);
            _output.localPositionOffset.y += _actionBobY;
            _output.localPositionOffset.x += _actionBobX;

            ApplyTransportCameraMotionScale(in input);

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
                RecoverCinematicImpulse(ref _collisionShakeY, ref _collisionShakeYVel, omega, dt, 0.0001f);
            }
            else { _collisionShakeY = 0f; _collisionShakeYVel = 0f; }

            if (math.abs(_collisionShakeX) > 0.0001f || math.abs(_collisionShakeXVel) > 0.0001f)
            {
                RecoverCinematicImpulse(ref _collisionShakeX, ref _collisionShakeXVel, omega, dt, 0.0001f);
            }
            else { _collisionShakeX = 0f; _collisionShakeXVel = 0f; }

            if (math.abs(_collisionShakePitch) > 0.001f || math.abs(_collisionShakePitchVel) > 0.001f)
            {
                RecoverCinematicImpulse(ref _collisionShakePitch, ref _collisionShakePitchVel, omega, dt, 0.001f);
            }
            else { _collisionShakePitch = 0f; _collisionShakePitchVel = 0f; }

            if (math.abs(_externalRollImpulse) > 0.001f || math.abs(_externalRollImpulseVel) > 0.001f)
            {
                RecoverCinematicImpulse(ref _externalRollImpulse, ref _externalRollImpulseVel, omega * 0.78f, dt, 0.001f);
            }
            else { _externalRollImpulse = 0f; _externalRollImpulseVel = 0f; }
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
        //  ACTION BOB (EATING, HEALING)
        // ══════════════════════════════════════════════════════════

        private void ProcessActionBob(float dt)
        {
            const float ACTION_BOB_OMEGA = 8f;

            if (math.abs(_actionBobY) > 0.0001f || math.abs(_actionBobYVel) > 0.0001f)
            {
                _actionBobY = SpringDamp(_actionBobY, 0f,
                    ref _actionBobYVel, ACTION_BOB_OMEGA, dt);
            }
            else { _actionBobY = 0f; _actionBobYVel = 0f; }

            if (math.abs(_actionBobX) > 0.0001f || math.abs(_actionBobXVel) > 0.0001f)
            {
                _actionBobX = SpringDamp(_actionBobX, 0f,
                    ref _actionBobXVel, ACTION_BOB_OMEGA, dt);
            }
            else { _actionBobX = 0f; _actionBobXVel = 0f; }
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

        private void ProcessWaterEntryFovImpulse(float dt)
        {
            if (_waterEntryFovTimer <= 0f || _waterEntryFovDuration <= 0f)
            {
                _waterEntryFovTimer = 0f;
                _waterEntryFovDuration = 0f;
                _waterEntryFovExpandDegrees = 0f;
                _waterEntryFovCompressDegrees = 0f;
                return;
            }

            _waterEntryFovTimer -= dt;
            if (_waterEntryFovTimer < 0f)
                _waterEntryFovTimer = 0f;

            float elapsed = _waterEntryFovDuration - _waterEntryFovTimer;
            float phase = math.saturate(elapsed / math.max(_waterEntryFovDuration, 0.01f));
            const float ExpandPhaseEnd = 0.32f;

            if (phase <= ExpandPhaseEnd)
            {
                float expandT = 1f - (phase / ExpandPhaseEnd);
                _output.fovOffset += _waterEntryFovExpandDegrees * expandT * expandT;
            }
            else
            {
                float compressT = 1f - ((phase - ExpandPhaseEnd) / math.max(1f - ExpandPhaseEnd, 0.01f));
                _output.fovOffset -= _waterEntryFovCompressDegrees * compressT * compressT;
            }

            if (_waterEntryFovTimer <= 0f)
            {
                _waterEntryFovDuration = 0f;
                _waterEntryFovExpandDegrees = 0f;
                _waterEntryFovCompressDegrees = 0f;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SWIM BOB
        // ══════════════════════════════════════════════════════════

        private void ProcessSpeedFovCheat(in CameraJuiceInput input, float dt)
        {
            float planarSpeed = math.max(0f, input.horizontalSpeed);
            float verticalSpeed = math.abs(input.verticalVelocity);
            float currentSpeed = math.max(
                math.max(planarSpeed, input.swimSpeed),
                ApproximateTwoAxisMagnitude(planarSpeed, verticalSpeed));
            float speed01 = math.saturate(
                (currentSpeed - SPEED_FOV_START_METERS_PER_SECOND) /
                math.max(0.01f, SPEED_FOV_FULL_METERS_PER_SECOND - SPEED_FOV_START_METERS_PER_SECOND));
            speed01 = speed01 * speed01 * (3f - 2f * speed01);

            float targetOffset = speed01 * SPEED_FOV_MAX_DEGREES;
            float blend = FastDampingBlendT(SPEED_FOV_LERP_SHARPNESS, dt);
            _speedFovOffset = math.lerp(_speedFovOffset, targetOffset, blend);
            _output.fovOffset += _speedFovOffset;
        }

        private void ProcessAbyssalNoirPulse(in CameraJuiceInput input, float dt)
        {
            float depth01 = math.saturate(
                (input.depth - ABYSSAL_NOIR_PULSE_START_DEPTH_METERS) /
                math.max(0.01f, ABYSSAL_NOIR_PULSE_FULL_DEPTH_METERS - ABYSSAL_NOIR_PULSE_START_DEPTH_METERS));
            float immersion01 = math.saturate((input.immersionRatio - 0.6f) * 2.5f);
            float intensity = depth01 * depth01 * immersion01;
            if (intensity <= 0.0001f)
                return;

            _abyssalNoirPulsePhase += dt * math.lerp(0.17f, 0.42f, depth01);
            if (_abyssalNoirPulsePhase > 1000f)
                _abyssalNoirPulsePhase -= 1000f;

            float phase = _abyssalNoirPulsePhase * TWO_PI;
            float pressureWave = (SignedTriangleRadians(phase) * 0.72f) + (SignedTriangleRadians((phase * 2.73f) + 1.11f) * 0.28f);
            float pulse = pressureWave * intensity;
            _output.fovOffset -= pulse * ABYSSAL_NOIR_PULSE_MAX_FOV_COMPRESS;
            _output.rollOffset += pulse * ABYSSAL_NOIR_PULSE_ROLL_DEGREES;
            _output.pitchOffset += math.abs(pulse) * ABYSSAL_NOIR_PULSE_PITCH_DEGREES;
        }

        private void ProcessSwimBob(in CameraJuiceInput input, SuitData suit, float dt)
        {
            if (suit == null || !suit.enableSwimBob) return;

            float targetIntensity = math.max(input.hasMovementInput != 0 ? 1f : 0f, input.transportBoost01 * 0.85f);
            float blendT = FastDampingBlendT(suit.swimBobTransitionSpeed, dt);
            _swimBobIntensity = math.lerp(_swimBobIntensity, targetIntensity, blendT);

            if (_swimBobIntensity < DEAD_ZONE) { _swimBobIntensity = 0f; return; }

            float maxSwim = suit.maxSwimSpeed > 0f ? suit.maxSwimSpeed : 9f;
            float speedFactor = math.saturate(input.swimSpeed / maxSwim);
            speedFactor = math.max(speedFactor, 0.3f);

            _swimBobTimer += suit.swimBobFrequency * TWO_PI * dt;
            if (_swimBobTimer > 100000f) _swimBobTimer -= 100000f;

            float scale = _swimBobIntensity * speedFactor;

            if (input.swimPresentationMode != PlayerSwimPresentationMode.None &&
                input.swimGuideWeight > DEAD_ZONE)
            {
                ProcessPresentationSynchronizedSwimBob(in input, suit, scale);
                return;
            }

            float verticalBob = SignedTriangleRadians(_swimBobTimer) * suit.swimBobVerticalAmplitude * scale;
            float forwardBob = SignedTriangleRadians(_swimBobTimer * 0.5f) * suit.swimBobForwardAmplitude * scale;
            float rollBob = SignedTriangleRadians(_swimBobTimer * 0.5f + HALF_PI) * suit.swimBobRollAmplitude * scale;

            _output.localPositionOffset.y += verticalBob;
            _output.localPositionOffset.z += forwardBob;
            _output.rollOffset += rollBob;
        }

        private void ProcessPresentationSynchronizedSwimBob(in CameraJuiceInput input, SuitData suit, float scale)
        {
            float modeScale = ResolvePresentationCameraScale(input.swimPresentationMode);
            float guideScale = math.saturate(input.swimGuideWeight);
            float transportBoost = math.saturate(input.transportBoost01);
            float propulsionScale = math.max(
                math.lerp(0.65f, 1f, math.saturate(input.swimPropulsionPulse)),
                math.lerp(0.72f, 1.08f, transportBoost));
            float finalScale = scale * modeScale * guideScale * propulsionScale;
            if (finalScale < DEAD_ZONE)
                return;

            float cycle = input.swimStrokePhase * TWO_PI;
            float verticalBob = SignedTriangleRadians(cycle) * suit.swimBobVerticalAmplitude * finalScale;
            float forwardBob = SignedTriangleRadians(cycle * 0.5f - 0.35f) * suit.swimBobForwardAmplitude * finalScale;
            float rollBob = SignedTriangleRadians(cycle + HALF_PI) * suit.swimBobRollAmplitude * finalScale;
            float pull = math.max(math.saturate(input.swimPropulsionPulse), transportBoost * TRANSPORT_PROPULSION_FLOOR);
            float pullKick = pull * pull;
            float pullImpulse = math.saturate(input.swimStrokeImpulse);
            float ascend = math.max(0f, input.swimVerticalInput);
            float descend = math.max(0f, -input.swimVerticalInput);
            float sprintClampScale = input.swimPresentationMode == PlayerSwimPresentationMode.UnderwaterSprint ? 0.82f : 1f;
            float propulsionPositionKick = math.min(
                (pullKick * SWIM_PRESENTATION_PROPULSION_POSITION_KICK +
                 pullImpulse * SWIM_PRESENTATION_PROPULSION_IMPULSE_POSITION_KICK) * modeScale,
                SWIM_PRESENTATION_MAX_POSITION_KICK * sprintClampScale);
            float propulsionFovKick = math.min(
                (pullKick * SWIM_PRESENTATION_PROPULSION_FOV_KICK +
                 pullImpulse * SWIM_PRESENTATION_PROPULSION_IMPULSE_FOV_KICK) * modeScale,
                SWIM_PRESENTATION_MAX_FOV_KICK * sprintClampScale);
            float transportPositionKick = transportBoost * TRANSPORT_POSITION_KICK * modeScale;
            float transportFovKick = transportBoost * TRANSPORT_FOV_KICK * modeScale;

            _output.localPositionOffset.y += verticalBob;
            _output.localPositionOffset.z += forwardBob;
            _output.rollOffset += rollBob;
            _output.localPositionOffset.z += propulsionPositionKick + transportPositionKick;
            _output.localPositionOffset.y +=
                (descend * SWIM_PRESENTATION_VERTICAL_DESCEND_OFFSET -
                 ascend * SWIM_PRESENTATION_VERTICAL_ASCEND_OFFSET) *
                pull * modeScale;
            _output.pitchOffset +=
                (descend * SWIM_PRESENTATION_DESCEND_PITCH -
                 ascend * SWIM_PRESENTATION_ASCEND_PITCH) *
                pull * modeScale;
            _output.fovOffset += propulsionFovKick + transportFovKick;
        }

        private void ApplyTransportCameraMotionScale(in CameraJuiceInput input)
        {
            float cameraMotionScale = math.saturate(input.transportCameraMotionScale);
            if (cameraMotionScale >= 0.9999f)
                return;

            _output.localPositionOffset *= cameraMotionScale;
            _output.rollOffset *= cameraMotionScale;
            _output.pitchOffset *= cameraMotionScale;
            _output.fovOffset *= cameraMotionScale;
        }

        private static float ResolvePresentationCameraScale(PlayerSwimPresentationMode mode)
        {
            switch (mode)
            {
                case PlayerSwimPresentationMode.ShallowWade:
                    return 0.18f;

                case PlayerSwimPresentationMode.SurfaceTread:
                    return 0.34f;

                case PlayerSwimPresentationMode.SurfaceStroke:
                    return 0.58f;

                case PlayerSwimPresentationMode.UnderwaterNeutral:
                    return 0.42f;

                case PlayerSwimPresentationMode.UnderwaterStroke:
                    return 0.92f;

                case PlayerSwimPresentationMode.UnderwaterGlide:
                    return 0.46f;

                case PlayerSwimPresentationMode.UnderwaterSprint:
                    return 1.08f;

                default:
                    return 0f;
            }
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
                float sign = NextCinematicShakeSign();
                _exhaleNextInterval = suit.exhaleIntervalBase + sign * variation;
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

        private void ProcessHeadBob(in CameraJuiceInput input, SuitData suit, float dt, float amplitudeScale, float cadenceScale)
        {
            float targetIntensity = (input.isGrounded != 0 && input.hasMovementInput != 0) ? 1f : 0f;
            float blendT = FastDampingBlendT(suit.bobTransitionSpeed, dt);
            _bobIntensity = math.lerp(_bobIntensity, targetIntensity, blendT);

            if (_bobIntensity < DEAD_ZONE) { _bobIntensity = 0f; _wasInLowPhase = false; return; }

            float speedFactor = suit.maxWalkSpeed > 0f
                ? math.clamp(input.horizontalSpeed / suit.maxWalkSpeed, 0f, 1f) : 0f;

            _bobTimer += suit.bobFrequency * cadenceScale * TWO_PI * dt * speedFactor;
            if (_bobTimer > 100000f) _bobTimer -= 100000f;

            float sinVal = SignedTriangleRadians(_bobTimer);
            float cosVal = SignedTriangleRadians((_bobTimer * 0.5f) + HALF_PI);

            _output.localPositionOffset.x += cosVal * suit.bobHorizontalAmplitude * amplitudeScale * _bobIntensity;
            _output.localPositionOffset.y += sinVal * suit.bobVerticalAmplitude * amplitudeScale * _bobIntensity;

            bool inLowPhase = sinVal < BOB_STEP_PHASE_THRESHOLD;
            if (inLowPhase && !_wasInLowPhase && _bobIntensity > 0.5f)
                _output.stepEvent = 1;
            _wasInLowPhase = inLowPhase;
        }

        // ══════════════════════════════════════════════════════════
        //  LANDING IMPACT
        // ══════════════════════════════════════════════════════════

        private void ProcessLandingImpact(in CameraJuiceInput input, SuitData suit, float dt, float dipScale)
        {
            if (input.isGrounded != 0 && input.wasGroundedLastFrame == 0)
            {
                float fallSpeed = math.abs(_preLandingVerticalVelocity);
                if (fallSpeed >= suit.impactVelocityThreshold)
                {
                    float norm = math.clamp(
                        (fallSpeed - suit.impactVelocityThreshold) /
                        math.max(suit.impactVelocityMax - suit.impactVelocityThreshold, 0.1f),
                        0f, 1f);
                    float scaledDip = suit.impactDipMaxDistance * dipScale;
                    _impactDipCurrent = -norm * scaledDip;
                    _impactDipVelocity = norm * scaledDip * 2f;
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

            float wave1 = SignedTriangleRadians(_surfaceBobTimer * 0.7f) * 0.05f;
            float wave2 = SignedTriangleRadians(_surfaceBobTimer * 1.1f + 2.1f) * 0.025f;
            float wave3 = SignedTriangleRadians(_surfaceBobTimer * 0.3f + 4.7f) * 0.015f;
            float waveRoll = SignedTriangleRadians(_surfaceBobTimer * 0.4f + 0.7f) * 1.5f;
            float wavePitch = SignedTriangleRadians(_surfaceBobTimer * 0.55f + 1.3f) * 0.6f;

            float finalIntensity = intensity;

            if (input.hasMovementInput != 0)
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

            float targetSwayIntensity = input.hasMovementInput != 0 ? 0f : 1f;
            float swayBlendT = FastDampingBlendT(suit.idleSwayTransitionSpeed, dt);
            _swayIntensity = math.lerp(_swayIntensity, targetSwayIntensity, swayBlendT);

            if (_swayIntensity < DEAD_ZONE) { _swayIntensity = 0f; return; }

            _swayTimer += dt;
            if (_swayTimer > 100000f) _swayTimer -= 100000f;

            float scale = _swayIntensity * depthScale;

            float swayY = SignedTriangleRadians(_swayTimer * suit.idleSwayFrequencyY * TWO_PI)
                        * suit.idleSwayAmplitudeY * scale;
            float swayX = SignedTriangleRadians(_swayTimer * suit.idleSwayFrequencyX * TWO_PI)
                        * suit.idleSwayAmplitudeX * scale;
            float swayRoll = SignedTriangleRadians(_swayTimer * suit.idleSwayFrequencyRoll * TWO_PI)
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
                float swayT = FastDampingBlendT(suit.idleSwayTransitionSpeed, dt);
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
                float swimBobT = FastDampingBlendT(suit != null ? suit.swimBobTransitionSpeed : 4f, dt);
                _swimBobIntensity = math.lerp(_swimBobIntensity, 0f, swimBobT);
            }
            else { _swimBobIntensity = 0f; }

            RecoverExhaleSpring(dt);
        }

        private void DecayWalkEffects(float dt, SuitData suit)
        {
            if (_bobIntensity > DEAD_ZONE)
            {
                float bobT = FastDampingBlendT(suit.bobTransitionSpeed, dt);
                _bobIntensity = math.lerp(_bobIntensity, 0f, bobT);
                if (_bobIntensity > DEAD_ZONE)
                {
                    float sinVal = SignedTriangleRadians(_bobTimer);
                    float cosVal = SignedTriangleRadians((_bobTimer * 0.5f) + HALF_PI);
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

        private float NextCinematicShakeSign()
        {
            _cinematicShakeSign = _cinematicShakeSign >= 0f ? -1f : 1f;
            return _cinematicShakeSign;
        }

        private static void RecoverCinematicImpulse(
            ref float current,
            ref float velocity,
            float sharpness,
            float dt,
            float epsilon)
        {
            float blend = FastDampingBlendT(math.max(0.01f, sharpness), dt);
            current = math.lerp(current, 0f, blend);
            velocity = 0f;
            if (math.abs(current) <= epsilon)
                current = 0f;
        }

        private static float FastDampingBlendT(float sharpness, float dt)
        {
            float x = math.max(0f, sharpness) * math.max(0f, dt);
            float x2 = x * x;
            float invExp = 1f / math.max(1f + x + (0.48f * x2) + (0.235f * x2 * x), 0.0001f);
            return math.saturate(1f - invExp);
        }

        private static float ApproximateTwoAxisMagnitude(float x, float y)
        {
            float ax = math.abs(x);
            float ay = math.abs(y);
            float max = math.max(ax, ay);
            float min = math.min(ax, ay);
            return max + (0.375f * min);
        }

        private static float SignedTriangleRadians(float radians)
        {
            float wrapped = math.frac(radians * INV_TWO_PI + 0.25f);
            return (1f - math.abs(wrapped * 2f - 1f)) * 2f - 1f;
        }

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
