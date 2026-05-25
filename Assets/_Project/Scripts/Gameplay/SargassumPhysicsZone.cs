// ============================================================================
// HECTON-8 — SargassumPhysicsZone.cs
// Sticky-drag trigger volume for dense sargassum clusters.
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Core;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Applies localized sticky drag to bodies moving through a sargassum cluster.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Gameplay/Sargassum Physics Zone")]
    public sealed class SargassumPhysicsZone : MonoBehaviour, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const uint KccVelocitySargassumMaxAgeFrames = 12u;
        private const uint CutRegistrationFrameStride = 6u;

        [Header("── Sticky Drag ─────────────────────────")]
        [Tooltip("Target max-speed multiplier while fully inside the sargassum mass.")]
        [SerializeField, Range(0.3f, 1f)] private float speedMultiplier = 0.68f;

        [Tooltip("Target drag multiplier while fully inside the sargassum mass.")]
        [SerializeField, Range(1f, 4f)] private float dragMultiplier = 2.15f;

        [Header("── Cut Response ───────────────────────")]
        [Tooltip("Optional responder that updates shader masking and debris bursts when a fast rigidbody cuts through the bush.")]
        [SerializeField] private SargassumCutResponder cutResponder;

        [Tooltip("Minimum rigidbody speed that counts as a cutting pass through the sargassum.")]
        [SerializeField, Range(0.1f, 20f)] private float cutSpeedThreshold = 3.6f;

        [Tooltip("World-space radius passed into the cut responder.")]
        [SerializeField, Range(0.1f, 4f)] private float cutRadius = 0.85f;

        [Header("── Diagnostics ─────────────────────────")]
        [SerializeField] private int _debugInfluencedBodies;

        private Collider _triggerCollider;
        private Transform _cachedTransform;
        private CachedTriggerVolume _cachedVolume;
        private IPlayerRuntimeContext _playerRuntime;
        private Transform _playerTransform;
        private SargassumMovementInfluence _playerInfluence;
        private bool _playerInside;
        private bool _registered;
        private bool _hotSwapRegistered;
        private uint _lastCutFrame;

        /// <summary>
        /// Configures the sticky-drag and cut-response defaults for this zone.
        /// </summary>
        /// <param name="responder">Optional cluster cut responder.</param>
        /// <param name="targetSpeedMultiplier">Sticky max-speed multiplier.</param>
        /// <param name="targetDragMultiplier">Sticky drag multiplier.</param>
        /// <param name="targetCutSpeedThreshold">Minimum cutter speed.</param>
        /// <param name="targetCutRadius">Responder cut radius.</param>
        public void Configure(
            SargassumCutResponder responder,
            float targetSpeedMultiplier,
            float targetDragMultiplier,
            float targetCutSpeedThreshold,
            float targetCutRadius)
        {
            cutResponder = responder;
            speedMultiplier = math.clamp(targetSpeedMultiplier, 0.1f, 1f);
            dragMultiplier = math.max(1f, targetDragMultiplier);
            cutSpeedThreshold = math.max(0.1f, targetCutSpeedThreshold);
            cutRadius = math.max(0.1f, targetCutRadius);
        }

        private void Reset()
        {
            TryGetComponent(out _triggerCollider);
            if (_triggerCollider != null)
                _triggerCollider.isTrigger = true;
            _cachedVolume = CachedTriggerVolume.FromCollider(_triggerCollider, cutRadius);
        }

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _triggerCollider);
            if (_triggerCollider != null)
                _triggerCollider.isTrigger = true;
            _cachedVolume = CachedTriggerVolume.FromCollider(_triggerCollider, cutRadius);
            RefreshPlayerReferencesCold();
        }

        private void OnEnable()
        {
            RefreshPlayerReferencesCold();
            TryRegister();
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            ClearPlayerInfluence();
            TryUnregisterHotSwapListener();
            TryUnregister();
            _debugInfluencedBodies = 0;
        }

        private void OnDestroy()
        {
            ClearPlayerInfluence();
            TryUnregisterHotSwapListener();
            TryUnregister();
        }

        public void Tick(float deltaTime)
        {
            Transform playerTransform = _playerTransform;
            SargassumMovementInfluence influence = _playerInfluence;
            if (playerTransform == null && _playerRuntime != null)
            {
                playerTransform = _playerRuntime.PlayerTransform;
                _playerTransform = playerTransform;
            }

            bool playerInside = playerTransform != null &&
                                influence != null &&
                                _cachedVolume.Contains(_cachedTransform, playerTransform.position);

            if (playerInside)
            {
                if (_playerInside)
                    influence.StayZone(speedMultiplier, dragMultiplier);
                else
                    EnterPlayerInfluence(influence);

                TryRegisterPlayerCut(playerTransform);
                return;
            }

            if (_playerInside)
                ExitPlayerInfluence();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                RefreshPlayerReferencesCold(currentService as IPlayerRuntimeContext, false);
            else if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && currentService != null)
                TryRegister();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
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

        private void RefreshPlayerReferencesCold(IPlayerRuntimeContext playerContext = null, bool useRegistryFallback = true)
        {
            if (_playerInside)
                ExitPlayerInfluence();

            _playerRuntime = playerContext ?? (useRegistryFallback ? GlobalRegistry.Player : null);
            IPlayerRuntimeContext runtime = _playerRuntime;
            _playerTransform = runtime != null ? runtime.PlayerTransform : null;
            HectonPlayerMovement movement = runtime != null ? runtime.PlayerMovement : null;
            _playerInfluence = null;
            if (movement != null)
                movement.TryGetComponent(out _playerInfluence);

            if (_playerInfluence == null && _playerTransform != null)
                _playerTransform.TryGetComponent(out _playerInfluence);
        }

        private void EnterPlayerInfluence(SargassumMovementInfluence influence)
        {
            _playerInside = true;
            influence.EnterZone(speedMultiplier, dragMultiplier);
            _debugInfluencedBodies = 1;
        }

        private void ExitPlayerInfluence()
        {
            SargassumMovementInfluence influence = _playerInfluence;
            if (influence != null)
                influence.ExitZone();

            _playerInside = false;
            _debugInfluencedBodies = 0;
        }

        private void ClearPlayerInfluence()
        {
            if (_playerInside)
                ExitPlayerInfluence();

            _playerTransform = null;
            _playerInfluence = null;
        }

        private void TryRegisterPlayerCut(Transform playerTransform)
        {
            if (cutResponder == null || playerTransform == null)
                return;

            uint frame = SystemDispatcher.CurrentFrameId;
            if (_lastCutFrame != 0u && frame - _lastCutFrame < CutRegistrationFrameStride)
                return;

            if (!CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocitySargassumMaxAgeFrames, out Vector3 velocity))
                return;

            float speedSq = velocity.sqrMagnitude;
            float cutSpeedThresholdSq = cutSpeedThreshold * cutSpeedThreshold;
            if (speedSq < cutSpeedThresholdSq)
                return;

            float speed = speedSq * math.rsqrt(speedSq);
            Vector3 contactPoint = _cachedVolume.ResolveSurfacePoint(_cachedTransform, playerTransform.position);
            cutResponder.RegisterCut(contactPoint, velocity, speed);
            _lastCutFrame = frame;
        }
    }
}
