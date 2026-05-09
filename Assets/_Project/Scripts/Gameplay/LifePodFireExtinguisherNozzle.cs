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
    public sealed class LifePodFireExtinguisherNozzle : MonoBehaviour, IUpdatable
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

        /// <summary>
        /// True while the nozzle is actively feeding foam into the visor shader fake.
        /// </summary>
        public bool IsSpraying => _spraying;

        private void Awake()
        {
            CacheScalarConfig();
            ResolveColdReferences();
        }

        private void OnEnable()
        {
            CacheScalarConfig();
            ResolveColdReferences();
        }

        private void OnDisable()
        {
            _spraying = false;
            _playerReferenceTransform = null;
            _nextHapticPulseSeconds = 0f;
            ResetFoamFlowCache();
            InvalidateColdReferenceCache();
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
            _spraying = false;
            _playerReferenceTransform = null;
            _nextHapticPulseSeconds = 0f;
            ResetFoamFlowCache();
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
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!_spraying)
            {
                TryUnregisterTick();
                return;
            }

            float dt = SanitizeAtLeast(deltaTime, 0f);
            if (dt <= 0f)
                return;

            if (targetController == null)
            {
                EndSpray();
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
            if ((_coldReferenceSearchMask & ColdReferenceSearchAll) == ColdReferenceSearchAll)
                return;

            if ((_coldReferenceSearchMask & ColdReferenceTargetController) == 0u)
            {
                if (targetController == null)
                    targetController = GetComponentInParent<LifePodTactilePrologueController>();
                _coldReferenceSearchMask |= ColdReferenceTargetController;
            }

            if ((_coldReferenceSearchMask & ColdReferenceForwardReference) == 0u)
            {
                if (nozzleForwardReference == null)
                    nozzleForwardReference = transform;
                _coldReferenceSearchMask |= ColdReferenceForwardReference;
            }
        }

        private float2 ResolveFoamFlowDirection()
        {
            Transform nozzle = nozzleForwardReference != null ? nozzleForwardReference : transform;
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

        private static Transform ResolvePlayerReferenceTransform()
        {
            IPlayerRuntimeContext player = GlobalRegistry.Player;
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
            ToolHapticsRuntime.EnqueueSinusoidalCommand(
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
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheScalarConfig();
            InvalidateColdReferenceCache();
        }
#endif
    }
}
