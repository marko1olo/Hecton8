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

using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hecton8.VFX
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class LandingImpactVFX : MonoBehaviour
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
        private bool _hasChromatic;
        private bool _hasVignette;

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
                _baseVignetteIntensity = _vignette.intensity.value;
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
            // Reset post-processing to base values
            if (_hasChromatic)
                _chromatic.intensity.value = _baseChromaticIntensity;
            if (_hasVignette)
                _vignette.intensity.value = _baseVignetteIntensity;
        }

        // ══════════════════════════════════════════════════════════
        //  UPDATE — Landing detection + effect decay
        // ══════════════════════════════════════════════════════════

        private void Update()
        {
            if (playerMovement == null || _playerRb == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

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
                _holdTimer -= dt;
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

                    float t = 1f - math.exp(-speed * dt);
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
    }
}