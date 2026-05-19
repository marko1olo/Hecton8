// ============================================================================
// HECTON-8 — HostileFlora.cs
// Stationary turret plant (Tiger Plant equivalent) that shoots at the player.
//
// ARCHITECTURE:
//   • Standalone prop — uses ISlowTickable via GameTickManager (no Update).
//   • Distance-based aggro detection.
//   • Smooth rotation towards target using Quaternion.Slerp.
//   • Cooldown-based projectile spawning.
//
// ZERO GC:
//   • ISlowTickable.SlowTick() — called ~2x per second, no per-frame allocations.
//   • Cached Transform, aiming bone.
//   • CompareTag for player detection.
//   • Pre-cached player reference via tag.
//
// USAGE:
//   1. Place on plant GameObject with visual mesh.
//   2. Assign aimingBone Transform (the part that rotates to face player).
//   3. Assign projectilePrefab (must have FloraProjectile component).
//   4. Configure aggro radius and shoot cooldown.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// State machine states for hostile flora behavior.
    /// </summary>
    public enum FloraState
    {
        Idle,       // No target, waiting
        Tracking,   // Target in range, rotating to face
        Shooting,   // Firing projectile
        Cooldown    // Waiting for next shot
    }

    /// <summary>
    /// Stationary hostile plant that shoots projectiles at nearby players.
    /// Subnautica Tiger Plant equivalent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HostileFlora : MonoBehaviour, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DETECTION
        // ══════════════════════════════════════════════════════════

        [Header("── Detection ──────────────────────────────────")]
        [Tooltip("Radius within which player is detected.")]
        [SerializeField, Range(1f, 50f)] private float aggroRadius = 10f;

        [Tooltip("Minimum distance to maintain (won't shoot if too close).")]
        [SerializeField, Range(0f, 5f)] private float minDistance = 1f;

        [Tooltip("Layer mask for player detection.")]
        [SerializeField] private LayerMask playerLayerMask;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AIMING
        // ══════════════════════════════════════════════════════════

        [Header("── Aiming ─────────────────────────────────────")]
        [Tooltip("Transform that rotates to face the target.")]
        [SerializeField] private Transform aimingBone;

        [Tooltip("Speed of rotation towards target.")]
        [SerializeField, Range(0.1f, 10f)] private float rotationSpeed = 2f;

        [Tooltip("Maximum pitch angle (degrees) for aiming.")]
        [SerializeField, Range(0f, 90f)] private float maxPitchAngle = 45f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SHOOTING
        // ══════════════════════════════════════════════════════════

        [Header("── Shooting ────────────────────────────────────")]
        [Tooltip("Legacy visual projectile prefab. Fast combat shots are queued into BallisticsRuntime instead of spawned.")]
        [SerializeField] private GameObject projectilePrefab;

        [Tooltip("Transform where projectiles spawn.")]
        [SerializeField] private Transform muzzlePoint;

        [Tooltip("Time between shots in seconds.")]
        [SerializeField, Range(0.5f, 10f)] private float shootCooldown = 2f;

        [Tooltip("Initial speed of spawned projectile.")]
        [SerializeField, Range(1f, 30f)] private float projectileSpeed = 10f;

        [Tooltip("Inaccuracy in degrees (random spread).")]
        [SerializeField, Range(0f, 30f)] private float inaccuracy = 5f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ───────────────────────────────────────")]
        [Tooltip("Sound played when shooting.")]
        [SerializeField] private AudioClip shootSound;

        [Tooltip("Volume for shoot sound.")]
        [SerializeField, Range(0f, 1f)] private float shootVolume = 0.7f;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private Transform _playerTarget;
        private FloraState _state = FloraState.Idle;
        private float _cooldownTimer;
        private uint _shotSeed;
        private uint _shotOrdinal;
        private bool _isRegistered;
        private bool _playerFound;

        // Pre-cached player tag
        private const string PlayerTag = "Player";
        private const float FacingDotThresholdSq = 0.81f; // 0.9^2, avoids normalizing target vector.

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Current state of the flora.</summary>
        public FloraState State => _state;

        /// <summary>Is a target currently in range?</summary>
        public bool HasTarget => _playerTarget != null;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;
            _shotSeed = MixHash(unchecked((uint)EntityId.ToULong(GetEntityId())) ^ 0x48464C52u);

            // Use self as muzzle if not assigned
            if (muzzlePoint == null)
            {
                muzzlePoint = _transform;
            }

            // Use self as aiming bone if not assigned
            if (aimingBone == null)
            {
                aimingBone = _transform;
            }

            // Set default layer mask if not assigned
            if (playerLayerMask == 0)
            {
                playerLayerMask = HectonLayerMasks.PlayerLayerMask;
            }
        }

        private void OnEnable()
        {
            _cooldownTimer = 0f;
            _state = FloraState.Idle;

            RegisterToSlowTick();
        }

        private void OnDisable()
        {
            UnregisterFromSlowTick();

            // Clear player reference
            _playerTarget = null;
            _playerFound = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by GameTickManager ~2x per second.
        /// Handles target detection, aiming, and shooting.
        /// </summary>
        public void SlowTick()
        {
            // Update cooldown
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= 0.5f; // Approximate slow tick interval
            }

            // Find or update player target
            UpdateTarget();

            // State machine
            switch (_state)
            {
                case FloraState.Idle:
                    TickIdle();
                    break;

                case FloraState.Tracking:
                    TickTracking();
                    break;

                case FloraState.Cooldown:
                    TickCooldown();
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  STATE MACHINE
        // ══════════════════════════════════════════════════════════

        private void UpdateTarget()
        {
            if (_playerTarget != null && _playerTarget.gameObject.activeInHierarchy)
            {
                _playerFound = true;
                return;
            }

            Hecton8.World.WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTarget);
            _playerFound = _playerTarget != null;
        }

        private void TickIdle()
        {
            if (_playerTarget == null) return;

            Vector3 toTarget = _playerTarget.position - _transform.position;
            float distanceSq = toTarget.sqrMagnitude;
            float aggroRadiusSq = aggroRadius * aggroRadius;
            float minDistanceSq = minDistance * minDistance;

            if (distanceSq <= aggroRadiusSq && distanceSq >= minDistanceSq)
            {
                _state = FloraState.Tracking;
            }
        }

        private void TickTracking()
        {
            if (_playerTarget == null)
            {
                _state = FloraState.Idle;
                return;
            }

            Vector3 toTarget = _playerTarget.position - _transform.position;
            float distanceSq = toTarget.sqrMagnitude;
            float aggroRadiusSq = aggroRadius * aggroRadius;
            float minDistanceSq = minDistance * minDistance;

            // Check if target left range
            if (distanceSq > aggroRadiusSq || distanceSq < minDistanceSq)
            {
                _state = FloraState.Idle;
                return;
            }

            // Rotate towards target
            RotateTowardsTarget();

            // Check if facing target and cooldown ready
            if (IsFacingTarget() && _cooldownTimer <= 0f)
            {
                Shoot();
            }
        }

        private void TickCooldown()
        {
            if (_cooldownTimer <= 0f)
            {
                _state = FloraState.Tracking;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  AIMING
        // ══════════════════════════════════════════════════════════

        private void RotateTowardsTarget()
        {
            if (_playerTarget == null || aimingBone == null) return;

            Vector3 direction = _playerTarget.position - aimingBone.position;

            // Clamp pitch
            Vector3 flatDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (flatDirection.sqrMagnitude < 0.01f) return;

            float pitch = Vector3.SignedAngle(flatDirection, direction, Vector3.right);
            pitch = Mathf.Clamp(pitch, -maxPitchAngle, maxPitchAngle);

            // Calculate target rotation
            Quaternion yawRotation = Quaternion.LookRotation(flatDirection, Vector3.up);
            Quaternion pitchRotation = Quaternion.AngleAxis(-pitch, Vector3.right);
            Quaternion targetRotation = yawRotation * pitchRotation;

            // Smooth rotation
            aimingBone.rotation = Quaternion.Slerp(aimingBone.rotation, targetRotation, rotationSpeed * 0.5f);
        }

        private bool IsFacingTarget()
        {
            if (_playerTarget == null || aimingBone == null) return false;

            Vector3 toTarget = _playerTarget.position - aimingBone.position;
            float toTargetLengthSq = toTarget.sqrMagnitude;
            if (toTargetLengthSq <= 0.0001f) return false;

            Vector3 forward = aimingBone.forward;

            float dot = Vector3.Dot(forward, toTarget);
            return dot > 0f && dot * dot >= FacingDotThresholdSq * toTargetLengthSq; // Within ~25 degrees
        }

        // ══════════════════════════════════════════════════════════
        //  SHOOTING
        // ══════════════════════════════════════════════════════════

        private void Shoot()
        {
            _state = FloraState.Shooting;
            _cooldownTimer = shootCooldown;

            Transform muzzle = muzzlePoint != null ? muzzlePoint : _transform;
            Vector3 spawnPos = muzzle.position;
            Quaternion spawnRot = muzzle.rotation;

            float randomAngle = ResolveShotSpreadAngle();
            spawnRot = Quaternion.AngleAxis(randomAngle, Vector3.up) * spawnRot;

            Vector3 projectileVelocity = ResolveSafeProjectileVelocity(spawnRot, projectileSpeed);
            BallisticsRuntime.QueueTrajectoryFromVelocity(
                spawnPos,
                projectileVelocity,
                BallisticsRuntime.FloraSpikeMassKg,
                BallisticWeaponHashes.FloraSpike,
                _shotSeed,
                BallisticTrajectoryFlags.HostileFlora);

            // Play shoot sound
            if (shootSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(shootSound, spawnPos, shootVolume);
            }

            _state = FloraState.Cooldown;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Forces the flora to shoot immediately (for scripting).
        /// </summary>
        public void ForceShoot()
        {
            if (_cooldownTimer <= 0f)
            {
                Shoot();
            }
        }

        /// <summary>
        /// Sets a custom target (for testing or special gameplay).
        /// </summary>
        /// <param name="target">Target transform to aim at.</param>
        public void SetTarget(Transform target)
        {
            _playerTarget = target;
            _playerFound = target != null;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TICK REGISTRATION
        // ══════════════════════════════════════════════════════════

        private void RegisterToSlowTick()
        {
            if (_isRegistered) return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null) return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _isRegistered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void UnregisterFromSlowTick()
        {
            if (!_isRegistered) return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _isRegistered = false;
        }

        private static Vector3 ResolveSafeProjectileVelocity(Quaternion spawnRotation, float authoredSpeed)
        {
            Vector3 forward = spawnRotation * Vector3.forward;
            if (!IsFinite(forward))
                return Vector3.zero;

            Vector3 velocity = forward * math.max(0f, authoredSpeed);
            return IsFinite(velocity) ? velocity : Vector3.zero;
        }

        private float ResolveShotSpreadAngle()
        {
            float authoredSpread = math.max(0f, inaccuracy);
            if (authoredSpread <= 0f)
                return 0f;

            uint value = _shotSeed + (_shotOrdinal++ * 0x9E3779B9u);
            return ((HashToUnit01(value) * 2f) - 1f) * authoredSpread;
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

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 pos = transform.position;

            // Draw aggro radius
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(pos, aggroRadius);

            // Draw min distance
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(pos, minDistance);

            // Draw aiming direction
            if (aimingBone != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(aimingBone.position, aimingBone.forward * 2f);
            }

            // Draw muzzle point
            if (muzzlePoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(muzzlePoint.position, 0.1f);
            }
        }
#endif
    }
}

