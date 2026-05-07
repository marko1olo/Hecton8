namespace Hecton8.Interaction
{
    using Hecton8.Core;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Collider-backed LifePod strap receiver for VR hand contact and PC interaction fallback.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Interaction/LifePod Seat Strap Latch")]
    public sealed class LifePodSeatStrapLatch : MonoBehaviour, IInteractable, IPhysicalPanelButtonReceiver, IUpdatable
    {
        private const float MinimumHoldSeconds = 0.01f;

        [Header("Latch")]
        [SerializeField, Tooltip("Seat coordinator that owns two-strap state and player motor lock.")]
        private LifePodSeatStrapCoordinator coordinator;

        [SerializeField, Tooltip("Physical strap side represented by this latch.")]
        private LifePodSeatStrapSide strapSide;

        [SerializeField, Tooltip("Collider registered into the physical hand receiver registry.")]
        private Collider activationCollider;

        [SerializeField, Tooltip("Anchor consumed by the contextual IK rig when this strap is latched.")]
        private Transform handIkAnchor;

        [SerializeField, Min(MinimumHoldSeconds), Tooltip("Required continuous physical hand contact before the strap latches.")]
        private float requiredHoldSeconds = 0.18f;

        [SerializeField, Min(0f), Tooltip("Hold progress decay speed when physical hand contact breaks.")]
        private float holdDecaySecondsPerSecond = 0.36f;

        [SerializeField, Tooltip("Existing PC interaction has no hold event; this makes a PC interact complete the latch immediately.")]
        private bool pcInteractCompletesLatch = true;

        [Header("Visual")]
        [SerializeField, Tooltip("Optional strap visual rotated into the latched local orientation.")]
        private Transform strapVisual;

        [SerializeField, Tooltip("Latched local Euler rotation applied to the optional strap visual.")]
        private Vector3 latchedLocalEulerDegrees = new Vector3(-34f, 0f, 0f);

        [Header("Prompt")]
        [SerializeField, Tooltip("Cached prompt returned before this strap latches.")]
        private string availablePrompt = "Latch Strap";

        [SerializeField, Tooltip("Cached prompt returned after this strap latches.")]
        private string lockedPrompt = "Strap Locked";

        private InteractionHighlighter _highlighter;
        private Quaternion _idleLocalRotation;
        private bool _idleRotationCached;
        private bool _latched;
        private bool _registeredReceiver;
        private bool _registeredTick;
        private bool _contactThisTick;
        private float _holdProgressSeconds;
        private Vector3 _lastHandPosition;
        private PhysicalHandSide _lastHandSide;

        /// <summary>
        /// True after the latch completed its required hold.
        /// </summary>
        public bool IsLatched => _latched;

        /// <summary>
        /// Current hold progress in normalized 0..1 space.
        /// </summary>
        public float HoldProgress01
        {
            get
            {
                float safeRequired = math.max(requiredHoldSeconds, MinimumHoldSeconds);
                return math.saturate(_holdProgressSeconds / safeRequired);
            }
        }

        private void Awake()
        {
            CacheColdReferences();
            CacheIdleVisual();
            _lastHandSide = strapSide == LifePodSeatStrapSide.Left ? PhysicalHandSide.Left : PhysicalHandSide.Right;
        }

        private void OnEnable()
        {
            CacheColdReferences();
            RegisterReceiver();
        }

        private void OnDisable()
        {
            UnregisterReceiver();
            TryUnregisterTick();
            _contactThisTick = false;
            _holdProgressSeconds = 0f;
        }

        /// <inheritdoc />
        public void OnHoverStart()
        {
            if (_highlighter != null)
                _highlighter.SetHighlight(true);
        }

        /// <inheritdoc />
        public void OnHoverEnd()
        {
            if (_highlighter != null)
                _highlighter.SetHighlight(false);
        }

        /// <inheritdoc />
        public void Interact(Transform interactor)
        {
            if (_latched)
                return;

            Vector3 samplePosition = interactor != null ? interactor.position : transform.position;
            PhysicalHandSide fallbackSide = strapSide == LifePodSeatStrapSide.Left
                ? PhysicalHandSide.Left
                : PhysicalHandSide.Right;

            if (pcInteractCompletesLatch)
            {
                CompleteLatch(samplePosition, fallbackSide);
                return;
            }

            QueueHoldSample(samplePosition, fallbackSide);
        }

        /// <inheritdoc />
        public string GetInteractText()
        {
            return _latched ? lockedPrompt : availablePrompt;
        }

        /// <inheritdoc />
        public bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide)
        {
            if (_latched || !IsFinite(handPosition))
                return _latched;

            QueueHoldSample(handPosition, fallbackHandSide);
            return true;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_latched)
            {
                TryUnregisterTick();
                return;
            }

            float safeDeltaTime = math.max(0f, deltaTime);
            if (_contactThisTick)
            {
                _contactThisTick = false;
                _holdProgressSeconds = math.min(
                    math.max(requiredHoldSeconds, MinimumHoldSeconds),
                    _holdProgressSeconds + safeDeltaTime);

                if (_holdProgressSeconds >= math.max(requiredHoldSeconds, MinimumHoldSeconds))
                {
                    CompleteLatch(_lastHandPosition, _lastHandSide);
                    return;
                }
            }
            else
            {
                _holdProgressSeconds = math.max(
                    0f,
                    _holdProgressSeconds - holdDecaySecondsPerSecond * safeDeltaTime);

                if (_holdProgressSeconds <= 0f)
                    TryUnregisterTick();
            }
        }

        /// <summary>
        /// Clears this latch and restores its optional strap visual to the cached idle rotation.
        /// </summary>
        public void ResetLatchVisualState()
        {
            _latched = false;
            _holdProgressSeconds = 0f;
            _contactThisTick = false;
            if (strapVisual != null && _idleRotationCached)
                strapVisual.localRotation = _idleLocalRotation;
        }

        private void QueueHoldSample(Vector3 handPosition, PhysicalHandSide handSide)
        {
            _lastHandPosition = handPosition;
            _lastHandSide = handSide;
            _contactThisTick = true;
            TryRegisterTick();
        }

        private void CompleteLatch(Vector3 handPosition, PhysicalHandSide handSide)
        {
            if (_latched)
                return;

            if (coordinator == null || !coordinator.TryLatch(strapSide, handIkAnchor, handPosition, handSide))
                return;

            _latched = true;
            _holdProgressSeconds = math.max(requiredHoldSeconds, MinimumHoldSeconds);
            ApplyLatchedVisual();
            TryUnregisterTick();
        }

        private void ApplyLatchedVisual()
        {
            if (strapVisual == null)
                return;

            strapVisual.localRotation = Quaternion.Euler(latchedLocalEulerDegrees);
        }

        private void CacheColdReferences()
        {
            if (activationCollider == null)
                TryGetComponent(out activationCollider);

            if (coordinator == null)
                coordinator = GetComponentInParent<LifePodSeatStrapCoordinator>();

            if (handIkAnchor == null)
                handIkAnchor = transform;

            if (_highlighter == null)
                TryGetComponent(out _highlighter);
        }

        private void CacheIdleVisual()
        {
            if (strapVisual == null || _idleRotationCached)
                return;

            _idleLocalRotation = strapVisual.localRotation;
            _idleRotationCached = true;
        }

        private void RegisterReceiver()
        {
            if (_registeredReceiver || activationCollider == null)
                return;

            PhysicalHandReceiverRegistry.Register(activationCollider, this);
            _registeredReceiver = true;
        }

        private void UnregisterReceiver()
        {
            if (!_registeredReceiver)
                return;

            PhysicalHandReceiverRegistry.Unregister(activationCollider, this);
            _registeredReceiver = false;
        }

        private void TryRegisterTick()
        {
            if (_registeredTick || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredTick = true;
        }

        private void TryUnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredTick = false;
        }

        private static bool IsFinite(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3));
        }
    }
}
