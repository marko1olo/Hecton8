// ============================================================================
// HECTON-8 — PlayerThrusterAudio.cs
// Dynamic thruster/servo sound that responds to swimming velocity.
//
// DESIGN:
//   Continuous looping sound that scales in volume and pitch
//   based on player's current swim speed. Creates "servomotor" feel.
//
//   Idle: very quiet low hum (ambient thruster idle).
//   Moving: louder, higher pitch (proportional to speed).
//   Braking: brief pitch drop (deceleration feel).
//
//   Only active in swim mode. Fades out when walking.
//
// ZERO GC: No allocations. Cached references. Math only.
// ============================================================================

using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class PlayerThrusterAudio : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [SerializeField] private HectonPlayerMovement playerMovement;

        [Header("── Audio Clips ───────────────────────────────")]
        [Tooltip("Looping thruster/servo sound. Should be a seamless loop. " +
                 "Ideally 2-5 seconds of clean servo hum.")]
        [SerializeField] private AudioClip thrusterLoopClip;

        [Header("── Volume ────────────────────────────────────")]
        [Tooltip("Volume when completely idle (no input, no movement). " +
                 "Subtle background hum. 0 = silent when idle.")]
        [SerializeField, Range(0f, 0.5f)]
        private float idleVolume = 0.05f;

        [Tooltip("Volume at maximum swim speed.")]
        [SerializeField, Range(0f, 1f)]
        private float maxVolume = 0.6f;

        [Tooltip("How quickly volume responds to speed changes. " +
                 "Higher = snappier. Lower = more gradual spool-up.")]
        [SerializeField, Range(1f, 20f)]
        private float volumeResponseSpeed = 5f;

        [Header("── Pitch ─────────────────────────────────────")]
        [Tooltip("Base pitch when idle.")]
        [SerializeField, Range(0.3f, 1.5f)]
        private float idlePitch = 0.7f;

        [Tooltip("Pitch at maximum swim speed.")]
        [SerializeField, Range(0.5f, 3f)]
        private float maxPitch = 1.4f;

        [Tooltip("How quickly pitch responds to speed changes.")]
        [SerializeField, Range(1f, 20f)]
        private float pitchResponseSpeed = 4f;

        [Header("── Mode Transition ──────────────────────────")]
        [Tooltip("How quickly thruster fades out when entering walk mode " +
                 "or fades in when entering swim mode.")]
        [SerializeField, Range(1f, 15f)]
        private float modeFadeSpeed = 4f;

        // ══════════════════════════════════════════════════════════
        //  CACHED
        // ══════════════════════════════════════════════════════════

        private AudioSource _audioSource;
        private Rigidbody _playerRb;

        private float _currentVolume;
        private float _currentPitch;
        private float _modeBlend;       // 0 = walk (silent), 1 = swim (active)

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();

            _audioSource.clip = thrusterLoopClip;
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1f;
            _audioSource.volume = 0f;
            _audioSource.pitch = idlePitch;
            _audioSource.priority = 200; // lower priority than footsteps

            if (playerMovement != null)
            {
                _playerRb = playerMovement.GetComponent<Rigidbody>();
            }

            _currentVolume = 0f;
            _currentPitch = idlePitch;
            _modeBlend = 0f;
        }

        private void OnEnable()
        {
            if (thrusterLoopClip != null && _audioSource != null)
            {
                _audioSource.Play();
            }
        }

        private void OnDisable()
        {
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  UPDATE — runs every frame for smooth audio response
        //
        //  Not using ITickable because this is a self-contained
        //  audio component. Keeps it decoupled from GameTickManager.
        //  Unity's Update is fine for audio parameter smoothing.
        // ══════════════════════════════════════════════════════════

        private void Update()
        {
            if (playerMovement == null || _playerRb == null) return;
            if (thrusterLoopClip == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // ── Mode blend: swim=1, walk=0 ──
            float targetModeBlend = playerMovement.IsWalking ? 0f : 1f;
            float modeT = 1f - math.exp(-modeFadeSpeed * dt);
            _modeBlend = math.lerp(_modeBlend, targetModeBlend, modeT);

            // ── Speed factor: 0..1 based on current swim speed ──
            float speedFactor = 0f;
            if (_modeBlend > 0.01f)
            {
                Vector3 vel = _playerRb.linearVelocity;
                float speed = math.sqrt(
                    vel.x * vel.x + vel.y * vel.y + vel.z * vel.z);
                float maxSpeed = playerMovement.CurrentSuit != null
                    ? playerMovement.CurrentSuit.maxSwimSpeed
                    : 12f;
                speedFactor = maxSpeed > 0f
                    ? math.clamp(speed / maxSpeed, 0f, 1f)
                    : 0f;
            }

            // ── Target volume and pitch ──
            float targetVolume = math.lerp(idleVolume, maxVolume, speedFactor) * _modeBlend;
            float targetPitch = math.lerp(idlePitch, maxPitch, speedFactor);

            // ── Smooth interpolation ──
            float volT = 1f - math.exp(-volumeResponseSpeed * dt);
            float pitT = 1f - math.exp(-pitchResponseSpeed * dt);

            _currentVolume = math.lerp(_currentVolume, targetVolume, volT);
            _currentPitch = math.lerp(_currentPitch, targetPitch, pitT);

            // ── Apply ──
            _audioSource.volume = _currentVolume;
            _audioSource.pitch = _currentPitch;
        }
    }
}