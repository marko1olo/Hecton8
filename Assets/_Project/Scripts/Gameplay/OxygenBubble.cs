// ============================================================================
// HECTON-8 — OxygenBubble.cs
// Floating oxygen bubble that restores player oxygen on contact.
//
// ARCHITECTURE:
//   • Standalone prop — uses ITickable via GameTickManager (no Update).
//   • Floats upward with configurable speed.
//   • OnTriggerEnter for player detection.
//   • UnityEvent for oxygen restoration (decoupled from player reference).
//
// ZERO GC:
//   • ITickable.Tick() — no Update(), no allocations.
//   • Cached Transform, Rigidbody.
//   • State machine with enum (no coroutines).
//   • CompareTag for player detection (no string allocation).
//
// USAGE:
//   1. Create bubble prefab with sphere collider (trigger).
//   2. Assign this script and configure float speed.
//   3. Connect OnCollected event to HectonSurvivalSystem.RefillOxygen.
//   4. Assign to OxygenPlant.oxygenBubblePrefab.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// State machine states for bubble lifecycle.
    /// </summary>
    public enum BubbleState
    {
        Floating,    // Moving upward
        Collected,   // Player collected
        Expired      // Lifetime exceeded
    }

    /// <summary>
    /// Floating oxygen bubble that restores player oxygen on contact.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class OxygenBubble : MonoBehaviour, ITickable, IUpdatable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — MOVEMENT
        // ══════════════════════════════════════════════════════════

        [Header("── Movement ──────────────────────────────────")]
        [Tooltip("Upward float speed in meters per second.")]
        [SerializeField, Range(0.1f, 5f)] private float floatSpeed = 1f;

        [Tooltip("Random horizontal drift amplitude.")]
        [SerializeField, Range(0f, 1f)] private float driftAmplitude = 0.1f;

        [Tooltip("Drift oscillation frequency.")]
        [SerializeField, Range(0f, 5f)] private float driftFrequency = 1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — LIFETIME
        // ══════════════════════════════════════════════════════════

        [Header("── Lifetime ───────────────────────────────────")]
        [Tooltip("Maximum lifetime in seconds before auto-destroy.")]
        [SerializeField, Range(1f, 30f)] private float maxLifetime = 10f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — OXYGEN
        // ══════════════════════════════════════════════════════════

        [Header("── Oxygen ─────────────────────────────────────")]
        [Tooltip("Amount of oxygen to restore on collection.")]
        [SerializeField, Range(1f, 50f)] private float oxygenAmount = 15f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO / VFX
        // ══════════════════════════════════════════════════════════

        [Header("── Audio / VFX ────────────────────────────────")]
        [Tooltip("Sound played when bubble is collected.")]
        [SerializeField] private AudioClip collectSound;

        [Tooltip("Volume for collect sound.")]
        [SerializeField, Range(0f, 1f)] private float collectVolume = 0.6f;

        [Tooltip("Particle system to play on collection.")]
        [SerializeField] private ParticleSystem collectParticles;

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Events ─────────────────────────────────────")]
        [Tooltip("Invoked when player collects the bubble. Passes oxygen amount.")]
        [SerializeField] private UnityEvent<float> OnCollected;

        [Tooltip("Invoked when bubble expires without collection.")]
        [SerializeField] private UnityEvent OnExpired;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private Collider _collider;
        private BubbleState _state = BubbleState.Floating;
        private float _lifetimeTimer;
        private float _driftOffset;
        private bool _isRegistered;

        // Pre-cached player tag for CompareTag
        private const string PlayerTag = "Player";

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Current state of the bubble.</summary>
        public BubbleState State => _state;

        /// <summary>Oxygen amount this bubble provides.</summary>
        public float OxygenAmount => oxygenAmount;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;
            _collider = GetComponent<Collider>();

            // Ensure collider is a trigger
            if (_collider != null)
            {
                _collider.isTrigger = true;
            }

            // Random drift offset for variation
            _driftOffset = Random.Range(0f, 6.28f); // 0 to 2π
        }

        private void OnEnable()
        {
            _state = BubbleState.Floating;
            _lifetimeTimer = 0f;

            RegisterToTick();
        }

        private void OnDisable()
        {
            UnregisterFromTick();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by GameTickManager every frame.
        /// Handles upward movement and lifetime countdown.
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime.</param>
        public void Tick(float deltaTime)
        {
            if (_state != BubbleState.Floating) return;

            // Move upward with drift
            FloatUpward(deltaTime);

            // Lifetime countdown
            _lifetimeTimer += deltaTime;
            if (_lifetimeTimer >= maxLifetime)
            {
                Expire();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  TRIGGER DETECTION
        // ══════════════════════════════════════════════════════════

        private void OnTriggerEnter(Collider other)
        {
            if (_state != BubbleState.Floating) return;

            // Check for player using CompareTag (zero GC)
            if (other.CompareTag(PlayerTag))
            {
                Collect(other.transform);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  STATE MACHINE
        // ══════════════════════════════════════════════════════════

        private void FloatUpward(float deltaTime)
        {
            // Base upward movement
            Vector3 movement = Vector3.up * floatSpeed * deltaTime;

            // Add horizontal drift (sine wave)
            if (driftAmplitude > 0f)
            {
                float driftX = Mathf.Sin(Time.time * driftFrequency + _driftOffset) * driftAmplitude * deltaTime;
                float driftZ = Mathf.Cos(Time.time * driftFrequency * 0.7f + _driftOffset) * driftAmplitude * deltaTime;
                movement.x += driftX;
                movement.z += driftZ;
            }

            _transform.position += movement;
        }

        private void Collect(Transform collector)
        {
            _state = BubbleState.Collected;

            // Play collection effects
            PlayCollectEffects();

            // Fire event with oxygen amount
            OnCollected?.Invoke(oxygenAmount);

            // Despawn or destroy
            DespawnSelf();
        }

        private void Expire()
        {
            _state = BubbleState.Expired;

            // Fire expiry event
            OnExpired?.Invoke();

            // Despawn or destroy
            DespawnSelf();
        }

        // ══════════════════════════════════════════════════════════
        //  VFX / AUDIO
        // ══════════════════════════════════════════════════════════

        private void PlayCollectEffects()
        {
            Vector3 pos = _transform.position;

            // Play sound
            if (collectSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(collectSound, pos, collectVolume);
            }

            // Play particles
            if (collectParticles != null)
            {
                collectParticles.transform.position = pos;
                collectParticles.Play();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DESPAWN
        // ══════════════════════════════════════════════════════════

        private void DespawnSelf()
        {
            // Unregister from tick first
            UnregisterFromTick();

            // Try pool despawn
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null && TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
            {
                pool.Despawn(gameObject);
                return;
            }

            // Fallback: destroy
            Destroy(gameObject);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Sets the oxygen amount (for runtime configuration).
        /// </summary>
        /// <param name="amount">Oxygen amount to restore.</param>
        public void SetOxygenAmount(float amount)
        {
            oxygenAmount = Mathf.Max(0f, amount);
        }

        /// <summary>
        /// Resets the bubble for pooling reuse.
        /// </summary>
        public void ResetBubble()
        {
            _state = BubbleState.Floating;
            _lifetimeTimer = 0f;
            _driftOffset = Random.Range(0f, 6.28f);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TICK REGISTRATION
        // ══════════════════════════════════════════════════════════

        private void RegisterToTick()
        {
            if (_isRegistered) return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = true;
        }

        private void UnregisterFromTick()
        {
            if (!_isRegistered) return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw float direction
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.1f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, Vector3.up * floatSpeed);
        }
#endif
    }
}

