// ============================================================================
// HECTON-8 — HostileFlora.cs
// Stationary turret plant (Tiger Plant equivalent) that shoots at the player.
//
// ARCHITECTURE:
//   • Standalone prop — uses ISlowTickable via Core registry dispatcher (no Update).
//   • Distance-based aggro detection.
//   • Smooth rotation towards target using Quaternion.Slerp.
//   • Cooldown-based mathematical trajectory queueing.
//
// ZERO GC:
//   • ISlowTickable.SlowTick() — dispatcher slow lane, no per-frame allocations.
//   • Cached Transform, aiming bone.
//   • Core registry player lookup.
//   • Core registry player reference.
//
// USAGE:
//   1. Place on plant GameObject with visual mesh.
//   2. Assign aimingBone Transform (the part that rotates to face player).
//   3. Assign muzzlePoint and aimingBone.
//   4. Configure aggro radius and shoot cooldown.
// ============================================================================

using Hecton8.Core;
using Hecton8.World;
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
        Shooting,   // Queueing mathematical shot
        Cooldown    // Waiting for next shot
    }

    /// <summary>
    /// Stationary hostile plant that queues mathematical shots at nearby players.
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
        [Tooltip("Legacy visual shell reference retained for prefab compatibility. Combat authority ignores this field.")]
        [SerializeField] private GameObject projectilePrefab;

        [Tooltip("Transform used as the ballistic muzzle origin.")]
        [SerializeField] private Transform muzzlePoint;

        [Tooltip("Time between shots in seconds.")]
        [SerializeField, Range(0.5f, 10f)] private float shootCooldown = 2f;

        [Tooltip("Authored ballistic shot speed.")]
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
        private uint _sourceEntityId;
        private uint _shotSeed;
        private bool _isRegistered;
        private bool _playerFound;

        private const float FacingDotThresholdSq = 0.81f; // 0.9^2, avoids normalizing target vector.
        private const float NominalSlowTickSeconds = 0.1f;
        private const double SectorHashInvMeters = 0.001;

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
            _sourceEntityId = GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
            _shotSeed = MixHash(_sourceEntityId ^ 0x48464C52u);

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
        /// Called by the dispatcher slow lane.
        /// Handles target detection, aiming, and mathematical shot queueing.
        /// </summary>
        public void SlowTick()
        {
            // Update cooldown
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer = math.max(0f, _cooldownTimer - NominalSlowTickSeconds);
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

            IPlayerRuntimeContext player = PlayerRuntimeContextService.ActiveRuntimeContext;
            _playerTarget = player != null && player.IsInitialized ? player.PlayerTransform : null;
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
            pitch = math.clamp(pitch, -maxPitchAngle, maxPitchAngle);

            // Calculate target rotation
            Quaternion yawRotation = Quaternion.LookRotation(flatDirection, Vector3.up);
            Quaternion pitchRotation = Quaternion.AngleAxis(-pitch, Vector3.right);
            Quaternion targetRotation = yawRotation * pitchRotation;

            // Smooth rotation
            aimingBone.rotation = Quaternion.Slerp(
                aimingBone.rotation,
                targetRotation,
                math.saturate(rotationSpeed * NominalSlowTickSeconds));
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

            float randomAngle = ResolveShotSpreadAngle(spawnPos);
            spawnRot = Quaternion.AngleAxis(randomAngle, Vector3.up) * spawnRot;

            Vector3 projectileVelocity = ResolveSafeProjectileVelocity(spawnRot, projectileSpeed);
            BallisticsRuntime.QueueTrajectoryFromVelocity(
                spawnPos,
                projectileVelocity,
                BallisticsRuntime.FloraSpikeMassKg,
                BallisticWeaponHashes.FloraSpike,
                _sourceEntityId,
                BallisticTrajectoryFlags.HostileFlora);

            // Play shoot sound
            if (shootSound != null && Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance is Hecton8.Core.IAudioService audio)
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

        private float ResolveShotSpreadAngle(Vector3 spawnPos)
        {
            float authoredSpread = math.max(0f, inaccuracy);
            if (authoredSpread <= 0f)
                return 0f;

            uint sectorHash = ResolveSectorHash(spawnPos);
            uint frameCounter = BallisticsRuntime.ResolveNextSimulationFrameCounter();
            Unity.Mathematics.Random rng = CreateDeterministicShotRandom(sectorHash, frameCounter, _shotSeed);
            return rng.NextFloat(-authoredSpread, authoredSpread);
        }

        private static Unity.Mathematics.Random CreateDeterministicShotRandom(uint sectorHash, uint simulationFrameCounter, uint salt)
        {
            uint seed = MixHash(sectorHash ^ 0xB5297A4Du);
            seed = MixHash(seed ^ simulationFrameCounter);
            seed = MixHash(seed ^ salt);
            return new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
        }

        private static uint ResolveSectorHash(Vector3 worldPosition)
        {
            if (!TryResolveRuntimeAup(worldPosition, out AbsoluteUniversePosition positionAup))
                return 1u;

            double3 aup = positionAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(aup)))
                return 1u;

            int sectorX = (int)math.floor(aup.x * SectorHashInvMeters);
            int sectorY = (int)math.floor(aup.y * SectorHashInvMeters);
            int sectorZ = (int)math.floor(aup.z * SectorHashInvMeters);

            uint hash = MixHash(unchecked((uint)sectorX) ^ 0x8DA6B343u);
            hash = MixHash(hash ^ unchecked((uint)sectorY) ^ 0xD8163841u);
            hash = MixHash(hash ^ unchecked((uint)sectorZ) ^ 0xCB1AB31Fu);
            return hash == 0u ? 1u : hash;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
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

