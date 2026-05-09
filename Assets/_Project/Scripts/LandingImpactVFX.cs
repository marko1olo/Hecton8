// ============================================================================
// HECTON-8 — LandingImpactVFX.cs
// Triggers post-processing effects on heavy landing impacts.
//
// DESIGN:
//   Listens to player velocity changes via FixedUpdate polling.
//   When a heavy landing is detected (velocity spike + grounded transition),
//   applies temporary Chromatic Aberration and Vignette intensification
//   via Unity Post-Processing / URP Volume overrides.
//
//   Duration and intensity scale with impact severity and suit weight.
//
// REQUIRES: Unity URP Post-Processing or Built-in Post-Processing Stack v2.
//           Uses Volume component with Chromatic Aberration + Vignette overrides.
//
// ZERO GC: No allocations in Update. Cached profile references.
// ============================================================================

using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hecton8.VFX
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class LandingImpactVFX : MonoBehaviour, ITickable, IUpdatable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [SerializeField] private HectonPlayerMovement playerMovement;

        [Header("── Chromatic Aberration ──────────────────────")]
        [Tooltip("Maximum chromatic aberration intensity on heaviest impact. " +
                 "0.5-1.0 = dramatic. 0.2-0.4 = subtle.")]
        [SerializeField, Range(0f, 1f)]
        private float maxChromaticIntensity = 0.6f;

        [Header("── Vignette ──────────────────────────────────")]
        [Tooltip("Maximum vignette intensity added on impact. " +
                 "Stacks on top of base vignette if any.")]
        [SerializeField, Range(0f, 0.5f)]
        private float maxVignetteIntensity = 0.35f;

        [Header("── Timing ────────────────────────────────────")]
        [Tooltip("How quickly the effect fades after impact. " +
                 "Lower = longer 'concussion' effect. " +
                 "Light suit: 5-8 (quick recovery). Heavy: 2-3 (lingering).")]
        [SerializeField, Range(0.5f, 15f)]
        private float recoverySpeed = 4f;

        [Tooltip("Delay before recovery starts (seconds). " +
                 "Keeps the effect at peak for this duration before fading. " +
                 "Creates a 'stunned' moment.")]
        [SerializeField, Range(0f, 0.5f)]
        private float holdDuration = 0.08f;

        [Header("── Water Transition ─────────────────────────")]
        [Tooltip("Chromatic aberration boost when crossing into water.")]
        [SerializeField, Range(0f, 1f)]
        private float submergeChromaticIntensity = 0.22f;

        [Tooltip("Vignette boost when crossing into water.")]
        [SerializeField, Range(0f, 0.5f)]
        private float submergeVignetteIntensity = 0.16f;

        [Tooltip("How long the underwater entry impulse stays at peak before fading.")]
        [SerializeField, Range(0f, 0.5f)]
        private float submergeHoldDuration = 0.06f;

        [Tooltip("How quickly the underwater entry impulse fades.")]
        [SerializeField, Range(0.5f, 15f)]
        private float submergeRecoverySpeed = 6f;

        [Tooltip("Chromatic aberration boost when breaking back to the surface.")]
        [SerializeField, Range(0f, 1f)]
        private float surfaceBreakChromaticIntensity = 0.12f;

        [Tooltip("Vignette boost when breaking back to the surface.")]
        [SerializeField, Range(0f, 0.5f)]
        private float surfaceBreakVignetteIntensity = 0.08f;

        [Tooltip("How long the surface-break impulse stays at peak before fading.")]
        [SerializeField, Range(0f, 0.5f)]
        private float surfaceBreakHoldDuration = 0.04f;

        [Tooltip("How quickly the surface-break impulse fades.")]
        [SerializeField, Range(0.5f, 15f)]
        private float surfaceBreakRecoverySpeed = 7.5f;

        [Header("── Thermocline Transition ─────────────────────────")]
        [Tooltip("Chromatic aberration boost when the player punches through a sharp water-layer boundary.")]
        [SerializeField, Range(0f, 1f)]
        private float thermoclineChromaticIntensity = 0.16f;

        [Tooltip("Vignette boost when the player crosses a thermocline boundary.")]
        [SerializeField, Range(0f, 0.5f)]
        private float thermoclineVignetteIntensity = 0.1f;

        [Tooltip("How long the thermocline shock sits at peak before recovery begins.")]
        [SerializeField, Range(0f, 0.5f)]
        private float thermoclineHoldDuration = 0.08f;

        [Tooltip("How quickly the thermocline optical shock decays.")]
        [SerializeField, Range(0.5f, 15f)]
        private float thermoclineRecoverySpeed = 7.2f;

        [Header("── Underwater Stress ───────────────────")]
        [Tooltip("Extra chromatic aberration applied while near-surface storm turbulence is violently disorienting the player.")]
        [SerializeField, Range(0f, 1f)]
        private float underwaterStressChromaticIntensity = 0.18f;

        [Tooltip("Extra vignette intensity applied while underwater storm turbulence is disorienting the player.")]
        [SerializeField, Range(0f, 0.5f)]
        private float underwaterStressVignetteIntensity = 0.14f;

        [Tooltip("Vignette smoothness target while underwater turbulence stress is active.")]
        [SerializeField, Range(0f, 1f)]
        private float underwaterStressVignetteSmoothness = 0.72f;

        [Header("── Locomotion Mode Mix ──────────────────────")]
        [Tooltip("Interior walk impacts should feel slightly damped compared to hard exterior ground.")]
        [SerializeField, Range(0f, 1f)]
        private float dryInteriorImpactMultiplier = 0.82f;

        [Tooltip("Shallow-wade impacts should feel heavily cushioned by water.")]
        [SerializeField, Range(0f, 1f)]
        private float shallowWadeImpactMultiplier = 0.48f;

        // ══════════════════════════════════════════════════════════
        //  CACHED — Post-processing references (grabbed once)
        // ══════════════════════════════════════════════════════════

        private Volume _volume;
        private ChromaticAberration _chromatic;
        private Vignette _vignette;

        private Rigidbody _playerRb;

        // ══════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════

        private float _currentIntensity;    // 0..1 normalized impact intensity
        private float _holdTimer;           // countdown for hold phase
        private bool _wasGrounded;
        private float _preLandingVelocityY;

        private float _baseChromaticIntensity;
        private float _baseVignetteIntensity;
        private float _baseVignetteSmoothness;
        private bool _baseVignetteRounded;
        private bool _hasChromatic;
        private bool _hasVignette;
        private bool _registeredToTickManager;
        private float _waterTransitionIntensity;
        private float _waterTransitionHoldTimer;
        private float _waterTransitionChromaticScale;
        private float _waterTransitionVignetteScale;
        private float _waterTransitionRecoverySpeed;
        private float _thermoclineTransitionIntensity;
        private float _thermoclineTransitionHoldTimer;
        private float _thermoclineTransitionRecoverySpeed;
        private float _transportCockpitVignetteIntensity;
        private float _transportCockpitVignetteSmoothness;
        private bool _transportCockpitVignetteRounded;
        private float _transportCockpitChromaticIntensity;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _volume = GetComponent<Volume>();

            if (_volume.profile == null)
            {
                Debug.LogWarning("[LandingImpactVFX] Volume has no profile assigned.", this);
                enabled = false;
                return;
            }

            // ── Cache post-processing overrides ──
            _hasChromatic = _volume.profile.TryGet(out _chromatic);
            _hasVignette = _volume.profile.TryGet(out _vignette);

            if (_hasChromatic)
            {
                _chromatic.active = true;
                _chromatic.intensity.overrideState = true;
                _baseChromaticIntensity = _chromatic.intensity.value;
            }

            if (_hasVignette)
            {
                _vignette.active = true;
                _vignette.intensity.overrideState = true;
                _vignette.smoothness.overrideState = true;
                _vignette.rounded.overrideState = true;
                _baseVignetteIntensity = _vignette.intensity.value;
                _baseVignetteSmoothness = _vignette.smoothness.value;
                _baseVignetteRounded = _vignette.rounded.value;
                _transportCockpitVignetteSmoothness = _baseVignetteSmoothness;
                _transportCockpitVignetteRounded = _baseVignetteRounded;
            }

            if (!_hasChromatic && !_hasVignette)
            {
                Debug.LogWarning(
                    "[LandingImpactVFX] Volume profile has neither ChromaticAberration " +
                    "nor Vignette. Add at least one override.", this);
                enabled = false;
                return;
            }

            if (playerMovement != null)
            {
                _playerRb = playerMovement.GetComponent<Rigidbody>();
            }

            _currentIntensity = 0f;
            _holdTimer = 0f;
            _wasGrounded = false;
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();

            // Reset post-processing to base values
            if (_hasChromatic)
                _chromatic.intensity.value = _baseChromaticIntensity;
            if (_hasVignette)
            {
                _vignette.intensity.value = _baseVignetteIntensity;
                _vignette.smoothness.value = _baseVignetteSmoothness;
                _vignette.rounded.value = _baseVignetteRounded;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  UPDATE — Landing detection + effect decay
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            RegisterToTickManager();
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            UpdateLandingEffect(deltaTime);
            UpdateWaterTransitionEffect(deltaTime);
            UpdateThermoclineTransitionEffect(deltaTime);
            ApplyPostProcessing();
            return;

#if false
            bool isGrounded = playerMovement.IsGrounded;

            // ── Track pre-landing velocity (while airborne) ──
            if (!isGrounded)
            {
                _preLandingVelocityY = _playerRb.linearVelocity.y;
            }

            // ── Detect landing edge ──
            if (isGrounded && !_wasGrounded)
            {
                SuitData suit = playerMovement.CurrentSuit;
                if (suit != null)
                {
                    float fallSpeed = math.abs(_preLandingVelocityY);

                    if (fallSpeed >= suit.impactVelocityThreshold)
                    {
                        float range = math.max(
                            suit.impactVelocityMax - suit.impactVelocityThreshold, 0.1f);
                        float normalizedImpact = math.clamp(
                            (fallSpeed - suit.impactVelocityThreshold) / range,
                            0f, 1f);

                        _currentIntensity = normalizedImpact;
                        _holdTimer = holdDuration;
                    }
                }
            }

            _wasGrounded = isGrounded;

            // ── Hold phase: keep intensity at peak ──
            if (_holdTimer > 0f)
            {
                _holdTimer -= deltaTime;
            }
            else
            {
                // ── Recovery phase: decay intensity ──
                if (_currentIntensity > 0.001f)
                {
                    // Use suit-specific recovery if available
                    float speed = recoverySpeed;
                    SuitData suit = playerMovement.CurrentSuit;
                    if (suit != null)
                    {
                        speed = suit.impactRecoverySpeed;
                    }

                    float t = ResolveDecayBlend(speed, deltaTime);
                    _currentIntensity = math.lerp(_currentIntensity, 0f, t);
                }
                else
                {
                    _currentIntensity = 0f;
                }
            }

            // ── Apply to post-processing ──
            if (_hasChromatic)
            {
                _chromatic.intensity.value = _baseChromaticIntensity +
                    _currentIntensity * maxChromaticIntensity;
            }

            if (_hasVignette)
            {
                _vignette.intensity.value = _baseVignetteIntensity +
                    _currentIntensity * maxVignetteIntensity;
            }
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Current impact intensity (0..1). Useful for UI shake effects.</summary>
        public float CurrentImpactIntensity => _currentIntensity;

        /// <summary>
        /// Manually trigger an impact effect (e.g., explosion knockback).
        /// </summary>
        public void TriggerImpact(float normalizedIntensity)
        {
            _currentIntensity = math.clamp(normalizedIntensity, 0f, 1f);
            _holdTimer = holdDuration;
        }

        /// <summary>
        /// Triggers the short optical shock when the camera crosses into water.
        /// </summary>
        public void TriggerSubmergeImpulse()
        {
            TriggerWaterTransitionImpulse(
                submergeChromaticIntensity,
                submergeVignetteIntensity,
                submergeHoldDuration,
                submergeRecoverySpeed);
        }

        /// <summary>
        /// Triggers the short optical shock when the camera breaks back to the surface.
        /// </summary>
        public void TriggerSurfaceBreakImpulse()
        {
            TriggerWaterTransitionImpulse(
                surfaceBreakChromaticIntensity,
                surfaceBreakVignetteIntensity,
                surfaceBreakHoldDuration,
                surfaceBreakRecoverySpeed);
        }

        /// <summary>
        /// Triggers a short optical shock when the player crosses a sharp underwater layer boundary.
        /// </summary>
        public void TriggerThermoclineImpulse(float normalizedIntensity)
        {
            float clampedIntensity = math.saturate(normalizedIntensity);
            if (clampedIntensity <= 0f)
                return;

            _thermoclineTransitionIntensity = math.max(_thermoclineTransitionIntensity, clampedIntensity);
            _thermoclineTransitionHoldTimer = math.max(_thermoclineTransitionHoldTimer, thermoclineHoldDuration);
            _thermoclineTransitionRecoverySpeed = math.max(0.1f, thermoclineRecoverySpeed);
        }

        /// <summary>
        /// Applies a persistent cockpit overlay on top of impact and water-transition optics.
        /// </summary>
        public void SetTransportCockpitOverlay(
            float vignetteIntensity,
            float vignetteRoundness,
            float vignetteSmoothness,
            float chromaticIntensity)
        {
            _transportCockpitVignetteIntensity = math.max(0f, vignetteIntensity);
            _transportCockpitVignetteRounded = vignetteRoundness >= 0.5f;
            _transportCockpitVignetteSmoothness = math.saturate(vignetteSmoothness);
            _transportCockpitChromaticIntensity = math.max(0f, chromaticIntensity);
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredToTickManager = GlobalRegistry.Updatables.Contains(this);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredToTickManager = false;
        }

        private void UpdateLandingEffect(float deltaTime)
        {
            if (playerMovement == null || _playerRb == null)
                return;

            bool isGrounded = playerMovement.IsGrounded;

            if (!isGrounded)
            {
                _preLandingVelocityY = _playerRb.linearVelocity.y;
            }

            if (isGrounded && !_wasGrounded)
            {
                SuitData suit = playerMovement.CurrentSuit;
                if (suit != null)
                {
                    float fallSpeed = math.abs(_preLandingVelocityY);

                    if (fallSpeed >= suit.impactVelocityThreshold)
                    {
                        float range = math.max(
                            suit.impactVelocityMax - suit.impactVelocityThreshold, 0.1f);
                        float normalizedImpact = math.clamp(
                            (fallSpeed - suit.impactVelocityThreshold) / range,
                            0f, 1f);
                        normalizedImpact *= ResolveImpactMultiplier();

                        _currentIntensity = normalizedImpact;
                        _holdTimer = holdDuration;
                    }
                }
            }

            _wasGrounded = isGrounded;

            if (_holdTimer > 0f)
            {
                _holdTimer -= deltaTime;
                return;
            }

            if (_currentIntensity > 0.001f)
            {
                float speed = recoverySpeed;
                SuitData suit = playerMovement.CurrentSuit;
                if (suit != null)
                {
                    speed = suit.impactRecoverySpeed;
                }

                float t = ResolveDecayBlend(speed, deltaTime);
                _currentIntensity = math.lerp(_currentIntensity, 0f, t);
            }
            else
            {
                _currentIntensity = 0f;
            }
        }

        private void UpdateWaterTransitionEffect(float deltaTime)
        {
            if (_waterTransitionHoldTimer > 0f)
            {
                _waterTransitionHoldTimer -= deltaTime;
                return;
            }

            if (_waterTransitionIntensity > 0.001f)
            {
                float t = ResolveDecayBlend(_waterTransitionRecoverySpeed, deltaTime);
                _waterTransitionIntensity = math.lerp(_waterTransitionIntensity, 0f, t);
            }
            else
            {
                _waterTransitionIntensity = 0f;
            }
        }

        private void UpdateThermoclineTransitionEffect(float deltaTime)
        {
            if (_thermoclineTransitionHoldTimer > 0f)
            {
                _thermoclineTransitionHoldTimer -= deltaTime;
                return;
            }

            if (_thermoclineTransitionIntensity > 0.001f)
            {
                float t = ResolveDecayBlend(_thermoclineTransitionRecoverySpeed, deltaTime);
                _thermoclineTransitionIntensity = math.lerp(_thermoclineTransitionIntensity, 0f, t);
            }
            else
            {
                _thermoclineTransitionIntensity = 0f;
            }
        }

        private void ApplyPostProcessing()
        {
            float underwaterStressIntensity = playerMovement != null
                ? playerMovement.CurrentUnderwaterStressIntensity01
                : 0f;

            if (_hasChromatic)
            {
                _chromatic.intensity.value = _baseChromaticIntensity +
                    _currentIntensity * maxChromaticIntensity +
                    _waterTransitionIntensity * _waterTransitionChromaticScale +
                    _thermoclineTransitionIntensity * thermoclineChromaticIntensity +
                    _transportCockpitChromaticIntensity +
                    underwaterStressIntensity * underwaterStressChromaticIntensity;
            }

            if (_hasVignette)
            {
                _vignette.intensity.value = _baseVignetteIntensity +
                    _currentIntensity * maxVignetteIntensity +
                    _waterTransitionIntensity * _waterTransitionVignetteScale +
                    _thermoclineTransitionIntensity * thermoclineVignetteIntensity +
                    _transportCockpitVignetteIntensity +
                    underwaterStressIntensity * underwaterStressVignetteIntensity;

                if (_transportCockpitVignetteIntensity > 0.0001f)
                {
                    _vignette.smoothness.value = _transportCockpitVignetteSmoothness;
                    _vignette.rounded.value = _transportCockpitVignetteRounded;
                }
                else
                {
                    _vignette.smoothness.value = math.lerp(
                        _baseVignetteSmoothness,
                        underwaterStressVignetteSmoothness,
                        underwaterStressIntensity);
                    _vignette.rounded.value = _baseVignetteRounded;
                }
            }
        }

        private void TriggerWaterTransitionImpulse(
            float chromaticScale,
            float vignetteScale,
            float holdTime,
            float decaySpeed)
        {
            _waterTransitionIntensity = 1f;
            _waterTransitionHoldTimer = holdTime;
            _waterTransitionChromaticScale = math.max(0f, chromaticScale);
            _waterTransitionVignetteScale = math.max(0f, vignetteScale);
            _waterTransitionRecoverySpeed = math.max(0.1f, decaySpeed);
        }

        private float ResolveImpactMultiplier()
        {
            if (playerMovement == null)
                return 1f;

            switch (playerMovement.CurrentLocomotionMode)
            {
                case PlayerLocomotionMode.DryInteriorWalk:
                    return dryInteriorImpactMultiplier;

                case PlayerLocomotionMode.ShallowWadeWalk:
                    return shallowWadeImpactMultiplier;

                case PlayerLocomotionMode.ExosuitLocomotion:
                    return 1.35f;

                default:
                    return 1f;
            }
        }

        private static float ResolveDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            return math.saturate(x / (1f + x));
        }
    }
}
