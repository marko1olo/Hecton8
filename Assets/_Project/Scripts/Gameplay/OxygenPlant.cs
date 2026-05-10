// ============================================================================
// HECTON-8 — OxygenPlant.cs
// Brain Coral equivalent — releases oxygen bubbles at regular intervals.
//
// ARCHITECTURE:
//   • Standalone prop — uses ITickable via GameTickManager (no Update).
//   • Timer-based bubble spawning.
//   • Integrates with SpatialAudioManager for "bloop" sound.
//
// ZERO GC:
//   • ITickable.Tick() — no Update(), no allocations.
//   • Cached Transform, spawn point.
//   • Timer state machine (no coroutines).
//
// USAGE:
//   1. Place on plant GameObject with visual mesh.
//   2. Assign oxygenBubblePrefab (must have OxygenBubble component).
//   3. Assign spawnPoint Transform where bubbles appear.
//   4. Configure release interval and audio.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Oxygen-producing plant that releases bubbles at regular intervals.
    /// Subnautica Brain Coral equivalent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OxygenPlant : MonoBehaviour, ITickable, IUpdatable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SPAWN SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Spawn Settings ────────────────────────────")]
        [Tooltip("Prefab with OxygenBubble component to spawn.")]
        [SerializeField] private GameObject oxygenBubblePrefab;

        [Tooltip("Transform where bubbles spawn. If null, uses self.")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Time between bubble releases in seconds.")]
        [SerializeField, Range(1f, 30f)] private float releaseInterval = 5f;

        [Tooltip("Random variation in release timing (+/- seconds).")]
        [SerializeField, Range(0f, 3f)] private float releaseVariation = 0.5f;

        [Tooltip("Should bubbles spawn on Start?")]
        [SerializeField] private bool spawnOnEnable = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ─────────────────────────────────────")]
        [Tooltip("Sound played when bubble is released.")]
        [SerializeField] private AudioClip releaseSound;

        [Tooltip("Volume for release sound.")]
        [SerializeField, Range(0f, 1f)] private float releaseVolume = 0.5f;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private float _releaseTimer;
        private float _nextReleaseTime;
        private uint _releaseSeed;
        private uint _releaseOrdinal;
        private bool _isRegistered;
        private bool _poolMissingLogged;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;
            _releaseSeed = MixHash(unchecked((uint)EntityId.ToULong(GetEntityId())) ^ 0x4F58504Cu);

            // Use self as spawn point if not assigned
            if (spawnPoint == null)
            {
                spawnPoint = _transform;
            }

            // Initialize timer
            CalculateNextReleaseTime();
        }

        private void OnEnable()
        {
            if (spawnOnEnable)
            {
                _releaseTimer = 0f;
            }

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
        /// Handles release timer countdown.
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime.</param>
        public void Tick(float deltaTime)
        {
            _releaseTimer += deltaTime;

            if (_releaseTimer >= _nextReleaseTime)
            {
                ReleaseBubble();
                _releaseTimer = 0f;
                CalculateNextReleaseTime();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  BUBBLE RELEASE
        // ══════════════════════════════════════════════════════════

        private void ReleaseBubble()
        {
            if (oxygenBubblePrefab == null) return;

            Vector3 spawnPos = spawnPoint.position;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null)
            {
                if (!_poolMissingLogged)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[OxygenPlant] ObjectPoolManager unavailable. Bubble release skipped to avoid runtime Instantiate.", this);
#endif
                    _poolMissingLogged = true;
                }

                return;
            }

            GameObject bubble = pool.Spawn(oxygenBubblePrefab, spawnPos, Quaternion.identity);
            if (bubble == null) return;

            // Play release sound
            if (releaseSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(releaseSound, spawnPos, releaseVolume);
            }
        }

        private void CalculateNextReleaseTime()
        {
            float authoredVariation = Mathf.Max(0f, releaseVariation);
            float signedPhase = authoredVariation > 0f
                ? (HashToUnit01(_releaseSeed + (_releaseOrdinal++ * 0x9E3779B9u)) * 2f) - 1f
                : 0f;

            _nextReleaseTime = releaseInterval + (signedPhase * authoredVariation);
            _nextReleaseTime = Mathf.Max(0.5f, _nextReleaseTime); // Minimum 0.5s
        }

        private static float HashToUnit01(uint value)
        {
            return (MixHash(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static uint MixHash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Immediately releases a bubble (for external triggers).
        /// </summary>
        public void ForceRelease()
        {
            ReleaseBubble();
            _releaseTimer = 0f;
            CalculateNextReleaseTime();
        }

        /// <summary>
        /// Resets the release timer.
        /// </summary>
        public void ResetTimer()
        {
            _releaseTimer = 0f;
            CalculateNextReleaseTime();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TICK REGISTRATION
        // ══════════════════════════════════════════════════════════

        private void RegisterToTick()
        {
            if (_isRegistered) return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null) return;

            _isRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
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
            // Draw spawn point
            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.5f);
            Gizmos.DrawWireSphere(pos, 0.15f);
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.2f);
            Gizmos.DrawSphere(pos, 0.1f);

            // Draw upward arrow
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(pos, Vector3.up * 0.5f);
        }
#endif
    }
}

