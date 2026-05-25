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
    public sealed class HostileFlora : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
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
        private bool _lateFrameRegistered;
        private bool _playerFound;
        private bool _hotSwapRegistered;
        private bool _hasLogicalAimRotation;
        private bool _pendingShootAudio;
        private Vector3 _pendingShootAudioPosition;
        private Quaternion _logicalAimRotation;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IAudioService _audioService;

        private const float FacingDotThresholdSq = 0.81f; // 0.9^2, avoids normalizing target vector.
        private const float NominalSlowTickSeconds = 0.1f;
        private const float DegreesToRadians = 0.01745329252f;
        private const float TwoPi = 6.28318530718f;
        private const float HalfPi = 1.57079632679f;
        private const float Pi = 3.14159265359f;
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
            _sourceEntityId = RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
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
            _logicalAimRotation = aimingBone != null ? aimingBone.rotation : _transform.rotation;
            _hasLogicalAimRotation = true;
            _pendingShootAudio = false;

            CacheRegistryServicesCold();
            RegisterToSlowTick();
            RegisterToLateFrameTick();
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            UnregisterFromSlowTick();
            UnregisterFromLateFrameTick();

            // Clear player reference
            _playerTarget = null;
            _playerFound = false;
            _hasLogicalAimRotation = false;
            _pendingShootAudio = false;
            _playerRuntimeContext = null;
            _audioService = null;
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

            IPlayerRuntimeContext player = _playerRuntimeContext;
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

            float pitch = ResolvePitchDegreesNoSignedAngle(flatDirection, direction);
            pitch = math.clamp(pitch, -maxPitchAngle, maxPitchAngle);

            // Calculate target rotation
            Quaternion yawRotation = Quaternion.LookRotation(flatDirection, Vector3.up);
            Quaternion pitchRotation = ApproximateRotationDegreesNoTrig(-pitch, Vector3.right);
            Quaternion targetRotation = yawRotation * pitchRotation;

            Quaternion currentRotation = _hasLogicalAimRotation ? _logicalAimRotation : aimingBone.rotation;
            _logicalAimRotation = Quaternion.Slerp(
                currentRotation,
                targetRotation,
                math.saturate(rotationSpeed * NominalSlowTickSeconds));
            _hasLogicalAimRotation = true;
        }

        private bool IsFacingTarget()
        {
            if (_playerTarget == null || aimingBone == null) return false;

            Vector3 toTarget = _playerTarget.position - aimingBone.position;
            float toTargetLengthSq = toTarget.sqrMagnitude;
            if (toTargetLengthSq <= 0.0001f) return false;

            Vector3 forward = _hasLogicalAimRotation ? _logicalAimRotation * Vector3.forward : aimingBone.forward;

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
            Quaternion spawnRot = muzzle == aimingBone && _hasLogicalAimRotation ? _logicalAimRotation : muzzle.rotation;

            float randomAngle = ResolveShotSpreadAngle(spawnPos);
            spawnRot = ApproximateRotationDegreesNoTrig(randomAngle, Vector3.up) * spawnRot;

            Vector3 projectileVelocity = ResolveSafeProjectileVelocity(spawnRot, projectileSpeed);
            BallisticsRuntime.QueueTrajectoryFromVelocity(
                spawnPos,
                projectileVelocity,
                BallisticsRuntime.FloraSpikeMassKg,
                BallisticWeaponHashes.FloraSpike,
                _sourceEntityId,
                BallisticTrajectoryFlags.HostileFlora);

            _pendingShootAudioPosition = spawnPos;
            _pendingShootAudio = shootSound != null;

            _state = FloraState.Cooldown;
        }

        public void LateFrameTick()
        {
            if (_hasLogicalAimRotation && aimingBone != null)
                aimingBone.rotation = _logicalAimRotation;

            if (!_pendingShootAudio)
                return;

            _pendingShootAudio = false;
            IAudioService audio = _audioService;
            if (shootSound != null && audio != null && audio.IsInitialized)
                audio.PlayAtPoint(shootSound, _pendingShootAudioPosition, shootVolume);
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

            _isRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterFromSlowTick()
        {
            if (!_isRegistered) return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _isRegistered = false;
        }

        private void RegisterToLateFrameTick()
        {
            if (_lateFrameRegistered) return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null) return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterFromLateFrameTick()
        {
            if (!_lateFrameRegistered) return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            CachePlayerRuntimeContext(GlobalRegistry.Player);
            _audioService = Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance;
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            _playerRuntimeContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    Transform previousTarget = previousService is IPlayerRuntimeContext previousPlayer
                        ? previousPlayer.PlayerTransform
                        : null;
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    if (_playerTarget == null || ReferenceEquals(_playerTarget, previousTarget))
                        _playerTarget = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerTransform : null;
                    _playerFound = _playerTarget != null;
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    _audioService = currentService as IAudioService;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _isRegistered = false;
                    _lateFrameRegistered = false;
                    if (currentService != null)
                    {
                        RegisterToSlowTick();
                        RegisterToLateFrameTick();
                    }
                    break;
            }
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

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
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
        //  MATH HELPERS
        // ══════════════════════════════════════════════════════════

        private static float ResolvePitchDegreesNoSignedAngle(Vector3 flatDirection, Vector3 direction)
        {
            float lengthSq = direction.sqrMagnitude;
            if (lengthSq <= 0.0001f)
                return 0f;

            float invLength = math.rsqrt(lengthSq);
            float vertical01 = math.saturate(math.abs(direction.y) * invLength);
            float pitchMagnitude = FastAsinDegrees(vertical01);
            float signDot = Vector3.Dot(Vector3.right, Vector3.Cross(flatDirection, direction));
            float signedPitch = math.select(-pitchMagnitude, pitchMagnitude, signDot >= 0f);
            return math.select(0f, signedPitch, vertical01 > 0.000001f);
        }

        private static Quaternion ApproximateRotationDegreesNoTrig(float angleDegrees, Vector3 axis)
        {
            float axisLengthSq = axis.sqrMagnitude;
            if (axisLengthSq <= 0.000001f || !float.IsFinite(axisLengthSq))
                return Quaternion.identity;

            Vector3 safeAxis = axis * math.rsqrt(axisLengthSq);
            ApproximateSinCosFullNoTrig(math.select(0f, angleDegrees, math.isfinite(angleDegrees)) * DegreesToRadians * 0.5f, out float sinHalf, out float cosHalf);
            Quaternion rotation = new Quaternion(
                safeAxis.x * sinHalf,
                safeAxis.y * sinHalf,
                safeAxis.z * sinHalf,
                cosHalf);
            return NormalizeQuaternion(rotation);
        }

        private static void ApproximateSinCosFullNoTrig(float radians, out float sin, out float cos)
        {
            float x = radians - (TwoPi * math.round(radians / TwoPi));
            float cosSign = 1f;
            if (x > HalfPi)
            {
                x = Pi - x;
                cosSign = -1f;
            }
            else if (x < -HalfPi)
            {
                x = -Pi - x;
                cosSign = -1f;
            }

            float x2 = x * x;
            sin = x * (1f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = cosSign * (1f - (x2 * (0.5f - (x2 * 0.041666667f))));
        }

        private static Quaternion NormalizeQuaternion(Quaternion value)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.lengthsq(q);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return Quaternion.identity;

            q *= math.rsqrt(lengthSq);
            return new Quaternion(q.x, q.y, q.z, q.w);
        }

        private static float FastAsinDegrees(float value)
        {
            float x = math.clamp(value, -1f, 1f);
            float ax = math.abs(x);
            float oneMinus = math.max(0f, 1f - ax);
            float root = oneMinus * math.rsqrt(math.max(oneMinus, 0.000001f));
            float acosRadians = (((-0.0187293f * ax + 0.0742610f) * ax - 0.2121144f) * ax + 1.5707288f) * root;
            float asinRadians = 1.57079632679f - acosRadians;
            return math.select(asinRadians, -asinRadians, x < 0f) * 57.2957795131f;
        }

        //  EDITOR
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

