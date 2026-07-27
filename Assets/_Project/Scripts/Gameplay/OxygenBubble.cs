// ============================================================================
// HECTON-8 — OxygenBubble.cs
// Floating oxygen bubble that restores player oxygen on contact.
//
// ARCHITECTURE:
//   • Standalone prop — uses ITickable via GameTickManager (no Update).
//   • Floats upward with configurable speed.
//   • Dispatcher-polled radius check against the cached player runtime context.
//   • Oxygen is credited on the survival owner route (IPlayerRuntimeContext.SurvivalSystem);
//     the UnityEvent is a presentation fan-out only and never carries survival truth.
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
//   3. Optionally bind OnCollected to local VFX/audio only — oxygen delivery needs no wiring.
//   4. Assign to OxygenPlant.oxygenBubblePrefab.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using Unity.Mathematics;
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
    public sealed class OxygenBubble : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const uint OxygenDeliveryMissWarningHash = 0x4F32424Du; // "O2BM"
        private const uint OxygenBubbleContextHash = 0x4F324255u;       // "O2BU"

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

        [Tooltip("Fallback player collection radius used by dispatcher polling when no spherical trigger size is available.")]
        [SerializeField, Range(0.1f, 5f)] private float collectionRadius = 0.85f;

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
        [Tooltip("Presentation-only fan-out fired after the oxygen was credited. Passes the delivered oxygen amount.")]
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
        private float _driftPhase;
        private uint _driftSequence;
        private uint _driftSeedBase;
        private bool _isRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private IAudioService _audioService;
        private IObjectPoolService _objectPool;
        private IPlayerRuntimeContext _playerRuntime;
        private Vector3 _pendingRuntimePosition;
        private bool _runtimePositionDirty;
        private float _effectiveCollectionRadius;
        private bool _pendingCollectEffects;
        private bool _pendingDespawn;
        private Vector3 _pendingCollectPosition;
        private bool _oxygenDeliveryMissReported;

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
            TryGetComponent(out _collider);
            _driftSeedBase = unchecked((uint)EntityId.ToULong(GetEntityId()));

            // Ensure collider is a trigger
            if (_collider != null)
            {
                _collider.isTrigger = true;
            }

            _effectiveCollectionRadius = ResolveCollectionRadius(_collider, collectionRadius, _transform);
            _driftPhase = ResolveDeterministicDriftPhase(_driftSequence);
            CacheRegistryServicesCold();
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            CacheRegistryServicesCold();
            ClearPendingLifecycleWork();
            _state = BubbleState.Floating;
            _lifetimeTimer = 0f;
            _driftPhase = ResolveDeterministicDriftPhase(_driftSequence);

            RegisterToTick();
        }

        private void OnDisable()
        {
            UnregisterFromTick();
            ClearPendingLifecycleWork();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            UnregisterFromTick();
            TryUnregisterHotSwapListener();
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
            if (TryCollectPlayerByRuntimePosition())
                return;

            // Lifetime countdown
            _lifetimeTimer += deltaTime;
            if (_lifetimeTimer >= maxLifetime)
            {
                Expire();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PLAYER COLLECTION DETECTION
        // ══════════════════════════════════════════════════════════

        private bool TryCollectPlayerByRuntimePosition()
        {
            IPlayerRuntimeContext playerRuntime = _playerRuntime;
            if (playerRuntime == null)
                return false;

            Transform playerTransform = playerRuntime.PlayerTransform;
            Vector3 playerPosition;
            if (playerRuntime.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose))
            {
                playerPosition = new Vector3(pose.RuntimePosition.x, pose.RuntimePosition.y, pose.RuntimePosition.z);
            }
            else
            {
                if (playerTransform == null)
                    return false;

                playerPosition = playerTransform.position;
            }

            if (playerTransform != null && !playerTransform.CompareTag(PlayerTag))
                return false;

            Vector3 bubblePosition = _runtimePositionDirty
                ? _pendingRuntimePosition
                : (_transform != null ? _transform.position : Vector3.zero);
            float radius = math.max(0.01f, _effectiveCollectionRadius);
            if ((playerPosition - bubblePosition).sqrMagnitude > radius * radius)
                return false;

            return TryCollect(playerTransform);
        }

        // ══════════════════════════════════════════════════════════
        //  STATE MACHINE
        // ══════════════════════════════════════════════════════════

        private void FloatUpward(float deltaTime)
        {
            // Base upward movement
            Vector3 movement = Vector3.up * floatSpeed * deltaTime;

            // Add horizontal drift through a cheap triangle-wave visual fake.
            if (driftAmplitude > 0f)
            {
                float safeFrequency = math.max(0f, driftFrequency);
                _driftPhase = math.frac(_driftPhase + safeFrequency * math.max(0f, deltaTime));
                float driftX = EvaluateSignedTriangle(_driftPhase) * driftAmplitude * deltaTime;
                float driftZ = EvaluateSignedTriangle(math.frac((_driftPhase * 0.7f) + 0.25f)) * driftAmplitude * deltaTime;
                movement.x += driftX;
                movement.z += driftZ;
            }

            if (!_runtimePositionDirty)
                _pendingRuntimePosition = _transform != null ? _transform.position : Vector3.zero;

            _pendingRuntimePosition += movement;
            _runtimePositionDirty = true;
        }

        public void LateFrameTick()
        {
            if (_runtimePositionDirty)
            {
                _runtimePositionDirty = false;
                if (_transform != null)
                    _transform.position = _pendingRuntimePosition;
            }

            if (_pendingCollectEffects)
            {
                _pendingCollectEffects = false;
                PlayCollectEffects(_pendingCollectPosition);
            }

            if (_pendingDespawn)
            {
                _pendingDespawn = false;
                DespawnSelf();
            }
        }

        private static float EvaluateSignedTriangle(float phase01)
        {
            float phase = math.frac(phase01);
            return 1f - (4f * math.abs(phase - 0.5f));
        }

        /// <summary>
        /// Credits the authored oxygen charge to the survival owner and consumes the bubble.
        /// Returns false when the survival owner is unreachable so the charge is not destroyed
        /// and the bubble stays collectable until it expires on its own lifetime.
        /// </summary>
        /// <param name="collector">Transform that entered the collection radius.</param>
        private bool TryCollect(Transform collector)
        {
            HectonSurvivalSystem survival = ResolveCollectorSurvivalSystem(collector);
            if (survival == null)
            {
                ReportOxygenDeliveryMiss();
                return false;
            }

            float deliveredOxygen = ResolveDeliverableOxygen(oxygenAmount);
            if (deliveredOxygen <= 0f)
                return false;

            // Survival truth lands on the owner route before any presentation fires.
            survival.RefillOxygen(deliveredOxygen);

            _state = BubbleState.Collected;
            _oxygenDeliveryMissReported = false;

            QueueCollectEffects();

            // Presentation-only fan-out; the oxygen was already credited above.
            OnCollected?.Invoke(deliveredOxygen);

            _pendingDespawn = true;
            return true;
        }

        /// <summary>
        /// Resolves the survival owner for the collecting transform through the cold-cached
        /// player runtime context. No scene search, no allocation, no registry polling.
        /// </summary>
        /// <param name="collector">Transform that entered the collection radius.</param>
        /// <returns>Survival owner, or null when the collector is not the live player root.</returns>
        private HectonSurvivalSystem ResolveCollectorSurvivalSystem(Transform collector)
        {
            IPlayerRuntimeContext playerRuntime = _playerRuntime;
            if (playerRuntime == null || !playerRuntime.IsInitialized)
                return null;

            if (collector != null && !ReferenceEquals(collector, playerRuntime.PlayerTransform))
                return null;

            return playerRuntime.SurvivalSystem;
        }

        /// <summary>
        /// Guards the authored charge against NaN, infinity, and negative authoring.
        /// </summary>
        /// <param name="authoredAmount">Serialized oxygen charge.</param>
        /// <returns>Finite, non-negative oxygen amount.</returns>
        private static float ResolveDeliverableOxygen(float authoredAmount)
        {
            return math.isfinite(authoredAmount) ? math.max(0f, authoredAmount) : 0f;
        }

        /// <summary>
        /// Publishes one bounded telemetry warning per bubble life when the survival owner
        /// could not be reached, so a silently uncollectable refill source stays visible.
        /// </summary>
        private void ReportOxygenDeliveryMiss()
        {
            if (_oxygenDeliveryMissReported)
                return;

            _oxygenDeliveryMissReported = true;
            GlobalTelemetryBus.PublishPerformanceWarning(
                OxygenDeliveryMissWarningHash,
                OxygenBubbleContextHash,
                ResolveDeliverableOxygen(oxygenAmount));
        }

        private void Expire()
        {
            _state = BubbleState.Expired;

            // Fire expiry event
            OnExpired?.Invoke();

            _pendingDespawn = true;
        }

        // ══════════════════════════════════════════════════════════
        //  VFX / AUDIO
        // ══════════════════════════════════════════════════════════

        private void QueueCollectEffects()
        {
            _pendingCollectPosition = _transform != null ? _transform.position : transform.position;
            _pendingCollectEffects = true;
        }

        private void PlayCollectEffects(Vector3 pos)
        {
            // Play sound
            IAudioService audio = ResolveAudioService();
            if (collectSound != null && audio != null)
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
            if (TryResolveCachedObjectPool(out IObjectPoolService pool) &&
                pool.CanDespawnWithoutDestroy(gameObject))
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
            oxygenAmount = math.max(0f, amount);
        }

        /// <summary>
        /// Resets the bubble for pooling reuse.
        /// </summary>
        public void ResetBubble()
        {
            ClearPendingLifecycleWork();
            _state = BubbleState.Floating;
            _lifetimeTimer = 0f;
            _driftSequence++;
            _driftPhase = ResolveDeterministicDriftPhase(_driftSequence);
        }

        private void ClearPendingLifecycleWork()
        {
            _runtimePositionDirty = false;
            _pendingRuntimePosition = Vector3.zero;
            _pendingCollectEffects = false;
            _pendingDespawn = false;
            _oxygenDeliveryMissReported = false;
        }

        private float ResolveDeterministicDriftPhase(uint sequence)
        {
            uint seed = _driftSeedBase;
            seed ^= sequence * 0x9E3779B9u;
            return HashToUnit01(seed);
        }

        private static float HashToUnit01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) * (1f / 16777215f);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TICK REGISTRATION
        // ══════════════════════════════════════════════════════════

        private void RegisterToTick()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null) return;

            if (!_isRegistered)
                _isRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterFromTick()
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (_isRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _isRegistered = false;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

        private void CacheRegistryServicesCold()
        {
            CacheAudioService(GlobalRegistry.Audio);
            CacheObjectPoolService(null);
            _playerRuntime = GlobalRegistry.Player;
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                _objectPool = pool;
                return;
            }

            _objectPool = null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPool = resolved;
                pool = resolved;
                return true;
            }

            _objectPool = null;
            pool = null;
            return false;
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _audioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _audioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
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
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPoolService(currentService as ObjectPoolManager);
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntime = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    UnregisterFromTick();
                    if (currentService != null && isActiveAndEnabled)
                        RegisterToTick();
                    break;
            }
        }

        private static float ResolveCollectionRadius(Collider source, float fallbackRadius, Transform owner)
        {
            float scale = ResolveMaxAbsScale(owner);
            float safeFallback = math.max(0.01f, fallbackRadius) * scale;
            if (source is SphereCollider sphere)
                return math.max(0.01f, sphere.radius) * scale;

            if (source is CapsuleCollider capsule)
                return math.max(math.max(0.01f, capsule.radius), capsule.height * 0.5f) * scale;

            if (source is BoxCollider box)
            {
                Vector3 size = box.size;
                float3 halfExtents = new float3(
                    math.abs(size.x),
                    math.abs(size.y),
                    math.abs(size.z)) * (0.5f * scale);
                return math.max(0.01f, math.length(halfExtents));
            }

            return safeFallback;
        }

        private static float ResolveMaxAbsScale(Transform owner)
        {
            if (owner == null)
                return 1f;

            Vector3 scale = owner.lossyScale;
            return math.max(0.01f, math.max(math.abs(scale.x), math.max(math.abs(scale.y), math.abs(scale.z))));
        }

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

