namespace Hecton8.Gameplay
{
    using Hecton8.Core;
    using Hecton8.Tools;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Physical extinguisher nozzle bridge that feeds the LifePod visor foam shader fake.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/LifePod Fire Extinguisher Nozzle")]
    public sealed class LifePodFireExtinguisherNozzle : MonoBehaviour, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const byte HapticPrioritySpray = 1;
        private const byte BothMotorMask = 0x03;
        private const uint FoamFlowRefreshFrameMask = 0x3u;
        private const uint ColdReferenceTargetController = 1u << 0;
        private const uint ColdReferenceForwardReference = 1u << 1;
        private const uint ColdReferenceSearchAll = ColdReferenceTargetController | ColdReferenceForwardReference;
        private const float MaxToolHapticFrequencyHz = 60f;

        [Header("Foam")]
        [Tooltip("LifePod controller that owns the screen-space foam shader state.")]
        [SerializeField] private LifePodTactilePrologueController targetController;

        [Tooltip("Forward reference for estimating visor-space foam flow direction.")]
        [SerializeField] private Transform nozzleForwardReference;

        [Tooltip("Foam mask contribution per held second.")]
        [SerializeField, Min(0f)] private float foamPerSecond = 0.72f;

        [Header("Haptics")]
        [Tooltip("Emit haptic pulses while the extinguisher is spraying.")]
        [SerializeField] private bool hapticsEnabled = true;

        [Tooltip("Seconds between repeated spray haptic pulses.")]
        [SerializeField, Min(0.01f)] private float hapticPulseIntervalSeconds = 0.08f;

        [Tooltip("Low-frequency extinguisher spray motor intensity.")]
        [SerializeField, Range(0f, 1f)] private float hapticLowFrequency = 0.12f;

        [Tooltip("High-frequency extinguisher spray motor intensity.")]
        [SerializeField, Range(0f, 1f)] private float hapticHighFrequency = 0.28f;

        [Tooltip("Duration of each spray haptic pulse.")]
        [SerializeField, Min(0.01f)] private float hapticDurationSeconds = 0.045f;

        [Tooltip("Triangle-wave haptic frequency for spray texture.")]
        [SerializeField, Min(1f)] private float hapticFrequencyHz = 92f;

        private bool _spraying;
        private bool _registeredTick;
        private Transform _playerReferenceTransform;
        private float2 _cachedFoamFlowDirection = new float2(0f, 1f);
        private float _nextHapticPulseSeconds;
        private float _resolvedFoamPerSecond;
        private float _resolvedHapticPulseIntervalSeconds;
        private float _resolvedHapticLowFrequency;
        private float _resolvedHapticHighFrequency;
        private float _resolvedHapticDurationSeconds;
        private float _resolvedHapticFrequencyHz;
        private uint _foamFlowRefreshFrameCounter;
        private uint _coldReferenceSearchMask;
        private Transform _cachedTransform;
        private Transform _resolvedNozzleForwardReference;
        private IPlayerRuntimeContext _playerRuntime;
        private bool _registeredHotSwapListener;
        private bool _tickDormant;

        /// <summary>
        /// True while the nozzle is actively feeding foam into the visor shader fake.
        /// </summary>
        public bool IsSpraying => _spraying;

        private void Awake()
        {
            CacheScalarConfig();
            ResolveColdReferences();
            RefreshColdRegistryReferences();
        }

        private void OnEnable()
        {
            CacheScalarConfig();
            ResolveColdReferences();
            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            StopSprayState();
            InvalidateColdReferenceCache();
            TryUnregisterHotSwapListener();
            TryUnregisterTick();
        }

        /// <summary>
        /// Starts continuous extinguisher spray until <see cref="EndSpray"/> is called.
        /// </summary>
        public void BeginSpray()
        {
            if (_spraying)
                return;

            ResolveColdReferences();
            if (targetController == null)
                return;

            CacheScalarConfig();
            _spraying = true;
            _playerReferenceTransform = ResolvePlayerReferenceTransform();
            _tickDormant = false;
            _nextHapticPulseSeconds = 0f;
            ResetFoamFlowCache();
            RefreshCachedFoamFlowDirection();
            _foamFlowRefreshFrameCounter = 1u;
            TryRegisterTick();
        }

        /// <summary>
        /// Stops continuous extinguisher spray and unregisters the nozzle tick.
        /// </summary>
        public void EndSpray()
        {
            StopSprayState();
            TryUnregisterTick();
        }

        /// <summary>
        /// Sets the continuous spray state from an external physical trigger or tool input owner.
        /// </summary>
        /// <param name="spraying">True to spray, false to stop.</param>
        public void SetSpraying(bool spraying)
        {
            if (spraying)
                BeginSpray();
            else
                EndSpray();
        }

        /// <summary>
        /// Allows dynamic extinguisher rig wiring to re-run cold parent/reference lookup once.
        /// </summary>
        public void InvalidateColdReferenceCache()
        {
            _coldReferenceSearchMask = 0u;
            _resolvedNozzleForwardReference = null;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_tickDormant)
                return;

            if (!_spraying)
            {
                _tickDormant = true;
                return;
            }

            float dt = SanitizeAtLeast(deltaTime, 0f);
            if (dt <= 0f)
                return;

            if (targetController == null)
            {
                StopSprayState();
                return;
            }

            float foamDelta = _resolvedFoamPerSecond * dt;
            if (foamDelta <= 0f)
                return;

            RefreshCachedFoamFlowDirectionFrame();
            targetController.ApplyExtinguisherFoamCachedFlow(foamDelta, _cachedFoamFlowDirection);
            QueueSprayHaptic(dt);
        }

        private void RefreshCachedFoamFlowDirectionFrame()
        {
            if ((_foamFlowRefreshFrameCounter++ & FoamFlowRefreshFrameMask) != 0u)
                return;

            RefreshCachedFoamFlowDirection();
        }

        private void RefreshCachedFoamFlowDirection()
        {
            _cachedFoamFlowDirection = ResolveFoamFlowDirection();
        }

        private void ResetFoamFlowCache()
        {
            _cachedFoamFlowDirection = new float2(0f, 1f);
            _foamFlowRefreshFrameCounter = 0u;
        }

        private void ResolveColdReferences()
        {
            EnsureSelfTransform();
            if ((_coldReferenceSearchMask & ColdReferenceSearchAll) == ColdReferenceSearchAll)
                return;

            if ((_coldReferenceSearchMask & ColdReferenceTargetController) == 0u)
            {
                if (targetController == null)
                {
                    if (!TryGetComponent(out targetController))
                        targetController = GetComponentInParent<LifePodTactilePrologueController>();
                }
                _coldReferenceSearchMask |= ColdReferenceTargetController;
            }

            if ((_coldReferenceSearchMask & ColdReferenceForwardReference) == 0u)
            {
                if (nozzleForwardReference == null)
                    nozzleForwardReference = _cachedTransform;
                _resolvedNozzleForwardReference = nozzleForwardReference != null ? nozzleForwardReference : _cachedTransform;
                _coldReferenceSearchMask |= ColdReferenceForwardReference;
            }
        }

        private float2 ResolveFoamFlowDirection()
        {
            Transform nozzle = _resolvedNozzleForwardReference;
            if (nozzle == null)
                return new float2(0f, 1f);

            Vector3 forward = nozzle.forward;
            Transform reference = _playerReferenceTransform;
            if (reference != null)
                forward = reference.InverseTransformDirection(forward);

            float2 direction = new float2(forward.x, forward.y);
            float lengthSq = math.lengthsq(direction);
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(direction)))
                return new float2(0f, 1f);

            return direction * math.rsqrt(lengthSq);
        }

        private void EnsureSelfTransform()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;
        }

        private Transform ResolvePlayerReferenceTransform()
        {
            IPlayerRuntimeContext player = _playerRuntime;
            return player != null ? player.PlayerTransform : null;
        }

        private void QueueSprayHaptic(float dt)
        {
            if (!hapticsEnabled)
                return;

            _nextHapticPulseSeconds -= dt;
            if (_nextHapticPulseSeconds > 0f)
                return;

            _nextHapticPulseSeconds = _resolvedHapticPulseIntervalSeconds;
            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                _resolvedHapticLowFrequency,
                _resolvedHapticHighFrequency,
                _resolvedHapticDurationSeconds,
                _resolvedHapticFrequencyHz,
                HapticPrioritySpray,
                BothMotorMask);
        }

        private void CacheScalarConfig()
        {
            _resolvedFoamPerSecond = SanitizeAtLeast(foamPerSecond, 0f);
            _resolvedHapticPulseIntervalSeconds = SanitizeAtLeast(hapticPulseIntervalSeconds, 0.01f);
            _resolvedHapticLowFrequency = SaturateFinite01(hapticLowFrequency);
            _resolvedHapticHighFrequency = SaturateFinite01(hapticHighFrequency);
            _resolvedHapticDurationSeconds = SanitizeAtLeast(hapticDurationSeconds, 0.01f);
            _resolvedHapticFrequencyHz = math.clamp(
                SanitizeAtLeast(hapticFrequencyHz, 1f),
                1f,
                MaxToolHapticFrequencyHz);
        }

        private static float SaturateFinite01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeAtLeast(float value, float minimum)
        {
            float resolved = math.isfinite(value) ? value : minimum;
            return math.max(minimum, resolved);
        }

        private void TryRegisterTick()
        {
            if (_registeredTick || !Application.isPlaying)
                return;

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void TryUnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredTick = false;
        }

        private void RefreshColdRegistryReferences()
        {
            _playerRuntime = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntime = currentService as IPlayerRuntimeContext;
                    if (_spraying)
                        _playerReferenceTransform = ResolvePlayerReferenceTransform();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registeredTick = false;
                    if (currentService != null && _spraying)
                    {
                        _tickDormant = false;
                        TryRegisterTick();
                    }
                    break;
            }
        }

        private void StopSprayState()
        {
            _spraying = false;
            _playerReferenceTransform = null;
            _nextHapticPulseSeconds = 0f;
            _tickDormant = true;
            ResetFoamFlowCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheScalarConfig();
            InvalidateColdReferenceCache();
        }
#endif
    }
}
