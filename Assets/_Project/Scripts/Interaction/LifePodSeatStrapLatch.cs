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
    public sealed class LifePodSeatStrapLatch : MonoBehaviour, IInteractable, IInteractableTextProvider, IPhysicalPanelButtonReceiver, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const float MinimumHoldSeconds = 0.01f;
        private const float MaximumHoldSeconds = 2.0f;
        private const float MaximumHoldDecaySecondsPerSecond = 8.0f;
        private const float MaximumLatchDeltaSeconds = 0.05f;
        private const int MaxParentResolveDepth = 32;

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
        private Quaternion _latchedLocalRotation;
        private bool _idleRotationCached;
        private bool _latchedRotationCached;
        private bool _latched;
        private bool _registeredReceiver;
        private bool _registeredTick;
        private bool _registeredHotSwap;
        private bool _tickDormant;
        private bool _contactThisTick;
        private float _holdProgressSeconds;
        private float _resolvedRequiredHoldSeconds = MinimumHoldSeconds;
        private float _resolvedHoldDecaySecondsPerSecond;
        private Vector3 _lastHandPosition;
        private PhysicalHandSide _lastHandSide;
        private Collider _registeredCollider;

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
                return math.saturate(_holdProgressSeconds / _resolvedRequiredHoldSeconds);
            }
        }

        private void Awake()
        {
            CacheScalarConfig();
            CacheColdReferences();
            CacheIdleVisual();
            CacheLatchedVisualRotation();
            _lastHandSide = strapSide == LifePodSeatStrapSide.Left ? PhysicalHandSide.Left : PhysicalHandSide.Right;
        }

        private void OnEnable()
        {
            CacheScalarConfig();
            CacheColdReferences();
            CacheLatchedVisualRotation();
            TryRegisterHotSwapListener();
            InteractableRegistry.RegisterTree(this);
            RegisterReceiver();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            UnregisterReceiver();
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
            if (_highlighter != null)
                _highlighter.SetHighlight(false);
            _contactThisTick = false;
            _holdProgressSeconds = 0f;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            bool shouldRestoreTick = (_registeredTick && !_tickDormant) || ShouldRunLatchTick();
            TryUnregisterTick();
            if (shouldRestoreTick && currentService != null && isActiveAndEnabled)
                TryRegisterTick();
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
            if (!IsFinite(samplePosition))
                return;

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

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(_latched ? lockedPrompt : availablePrompt, destination, out length);
        }

        /// <inheritdoc />
        public bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide,
            int sampleFrame = -1)
        {
            if (_latched || !IsFinite(handPosition))
                return _latched;

            QueueHoldSample(handPosition, fallbackHandSide);
            return true;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_tickDormant)
                return;

            if (_latched)
            {
                _tickDormant = true;
                return;
            }

            float safeDeltaTime = SanitizeDeltaSeconds(deltaTime);
            if (_contactThisTick)
            {
                _contactThisTick = false;
                _holdProgressSeconds = math.min(
                    _resolvedRequiredHoldSeconds,
                    _holdProgressSeconds + safeDeltaTime);

                if (_holdProgressSeconds >= _resolvedRequiredHoldSeconds)
                {
                    CompleteLatch(_lastHandPosition, _lastHandSide, false);
                    return;
                }
            }
            else
            {
                _holdProgressSeconds = math.max(
                    0f,
                    _holdProgressSeconds - _resolvedHoldDecaySecondsPerSecond * safeDeltaTime);

                if (_holdProgressSeconds <= 0f)
                    _tickDormant = true;
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
            if (_highlighter != null)
                _highlighter.SetHighlight(false);
            if (strapVisual != null && _idleRotationCached)
                strapVisual.localRotation = _idleLocalRotation;
            TryUnregisterTick();
        }

        private void QueueHoldSample(Vector3 handPosition, PhysicalHandSide handSide)
        {
            if (!IsFinite(handPosition))
                return;

            _lastHandPosition = handPosition;
            _lastHandSide = handSide;
            _contactThisTick = true;
            _tickDormant = false;
            TryRegisterTick();
        }

        private void CompleteLatch(Vector3 handPosition, PhysicalHandSide handSide, bool unregisterTick = true)
        {
            if (_latched)
                return;

            if (coordinator == null || !coordinator.TryLatch(strapSide, handIkAnchor, handPosition, handSide))
                return;

            _latched = true;
            _holdProgressSeconds = _resolvedRequiredHoldSeconds;
            ApplyLatchedVisual();
            if (unregisterTick)
                TryUnregisterTick();
            else
                _tickDormant = true;
        }

        private void ApplyLatchedVisual()
        {
            if (strapVisual == null)
                return;

            if (!_latchedRotationCached)
                CacheLatchedVisualRotation();

            strapVisual.localRotation = _latchedLocalRotation;
        }

        private void CacheColdReferences()
        {
            if (activationCollider == null)
                TryGetComponent(out activationCollider);

            if (coordinator == null)
                TryResolveParentComponent(transform, out coordinator);

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

        private void CacheLatchedVisualRotation()
        {
            if (_latchedRotationCached)
                return;

            _latchedLocalRotation = ResolveEulerRotationNoTrig(SanitizeEulerDegrees(latchedLocalEulerDegrees));
            _latchedRotationCached = true;
        }

        private void RegisterReceiver()
        {
            if (activationCollider == null)
                return;

            if (_registeredReceiver)
            {
                if (ReferenceEquals(_registeredCollider, activationCollider))
                    return;

                UnregisterReceiver();
            }

            if (!Application.isPlaying || !PhysicalHandReceiverRegistry.TryRegister(activationCollider, this))
                return;

            _registeredCollider = activationCollider;
            _registeredReceiver = true;
        }

        private void UnregisterReceiver()
        {
            if (!_registeredReceiver)
                return;

            PhysicalHandReceiverRegistry.Unregister(_registeredCollider, this);
            _registeredCollider = null;
            _registeredReceiver = false;
        }

        private void TryRegisterTick()
        {
            if (_registeredTick || !Application.isPlaying)
                return;

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            if (_registeredTick)
                _tickDormant = false;
        }

        private void TryUnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredTick = false;
            _tickDormant = false;
        }

        private bool ShouldRunLatchTick()
        {
            return !_latched && (_contactThisTick || _holdProgressSeconds > 0f);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private static bool IsFinite(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3));
        }

        private static float SanitizeDeltaSeconds(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0f, MaximumLatchDeltaSeconds) : 0f;
        }

        private void CacheScalarConfig()
        {
            _resolvedRequiredHoldSeconds = ResolveSafeRequiredHoldSeconds();
            _resolvedHoldDecaySecondsPerSecond = ResolveSafeHoldDecaySecondsPerSecond();
        }

        private float ResolveSafeRequiredHoldSeconds()
        {
            return math.isfinite(requiredHoldSeconds)
                ? math.clamp(requiredHoldSeconds, MinimumHoldSeconds, MaximumHoldSeconds)
                : MinimumHoldSeconds;
        }

        private float ResolveSafeHoldDecaySecondsPerSecond()
        {
            return math.isfinite(holdDecaySecondsPerSecond)
                ? math.clamp(holdDecaySecondsPerSecond, 0f, MaximumHoldDecaySecondsPerSecond)
                : 0f;
        }

        private static Vector3 SanitizeEulerDegrees(Vector3 value)
        {
            if (!IsFinite(value))
                return Vector3.zero;

            return new Vector3(
                math.clamp(value.x, -360f, 360f),
                math.clamp(value.y, -360f, 360f),
                math.clamp(value.z, -360f, 360f));
        }

        private static bool TryResolveParentComponent<T>(Transform start, out T component) where T : Component
        {
            component = null;
            Transform current = start;
            int depth = 0;
            while (current != null && depth < MaxParentResolveDepth)
            {
                if (current.TryGetComponent(out component))
                    return true;

                current = current.parent;
                depth++;
            }

            return false;
        }

        private static Quaternion ResolveEulerRotationNoTrig(Vector3 eulerDegrees)
        {
            ApproximateSinCos(eulerDegrees.x * 0.00872664626f, out float sx, out float cx);
            ApproximateSinCos(eulerDegrees.y * 0.00872664626f, out float sy, out float cy);
            ApproximateSinCos(eulerDegrees.z * 0.00872664626f, out float sz, out float cz);

            float4 pitch = new float4(sx, 0f, 0f, cx);
            float4 yaw = new float4(0f, sy, 0f, cy);
            float4 roll = new float4(0f, 0f, sz, cz);
            float4 q = MulQuaternion(MulQuaternion(yaw, pitch), roll);
            q *= ApproximateInverseMagnitudeNoSqrt(q);
            return new Quaternion(q.x, q.y, q.z, q.w);
        }

        private static float ApproximateInverseMagnitudeNoSqrt(float4 value)
        {
            float4 absValue = math.abs(value);
            float largest = math.max(math.max(absValue.x, absValue.y), math.max(absValue.z, absValue.w));
            float smallest = math.min(math.min(absValue.x, absValue.y), math.min(absValue.z, absValue.w));
            float middleSum = absValue.x + absValue.y + absValue.z + absValue.w - largest - smallest;
            float magnitude = largest + (middleSum * 0.25f) + (smallest * 0.125f);
            return math.rcp(math.max(magnitude, 0.000001f));
        }

        private static void ApproximateSinCos(float radians, out float sin, out float cos)
        {
            const float pi = 3.14159265359f;
            const float twoPi = 6.28318530718f;
            const float halfPi = 1.57079632679f;

            float x = radians - (twoPi * math.round(radians / twoPi));
            float cosSign = 1f;
            if (x > halfPi)
            {
                x = pi - x;
                cosSign = -1f;
            }
            else if (x < -halfPi)
            {
                x = -pi - x;
                cosSign = -1f;
            }

            float x2 = x * x;
            sin = x * (1f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = cosSign * (1f - (x2 * (0.5f - (x2 * 0.041666667f))));
        }

        private static float4 MulQuaternion(float4 lhs, float4 rhs)
        {
            return new float4(
                lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.w * rhs.y - lhs.x * rhs.z + lhs.y * rhs.w + lhs.z * rhs.x,
                lhs.w * rhs.z + lhs.x * rhs.y - lhs.y * rhs.x + lhs.z * rhs.w,
                lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!math.isfinite(requiredHoldSeconds))
                requiredHoldSeconds = MinimumHoldSeconds;
            if (!math.isfinite(holdDecaySecondsPerSecond))
                holdDecaySecondsPerSecond = 0f;
            requiredHoldSeconds = math.clamp(requiredHoldSeconds, MinimumHoldSeconds, MaximumHoldSeconds);
            holdDecaySecondsPerSecond = math.clamp(holdDecaySecondsPerSecond, 0f, MaximumHoldDecaySecondsPerSecond);
            CacheScalarConfig();
            latchedLocalEulerDegrees = SanitizeEulerDegrees(latchedLocalEulerDegrees);
            _latchedRotationCached = false;
            CacheLatchedVisualRotation();
        }
#endif
    }
}
