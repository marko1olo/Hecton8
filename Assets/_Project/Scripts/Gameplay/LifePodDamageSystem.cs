namespace Hecton8.Gameplay
{
    using Hecton8.Core;
    using Hecton8.Tools;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// Water-impact LifePod damage mask that renders deterministic spark quads without particle simulation.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/LifePod Damage System")]
    public sealed class LifePodDamageSystem : MonoBehaviour, IUpdatable
    {
        private const int MaxShortCircuitBits = 16;
        private const int MaxVisibleSparkInstances = 4;
        private const int MaximumRenderLayer = 31;
        private const byte HapticPriorityCritical = ToolHapticsRuntime.PriorityCritical;
        private const byte BothMotorMask = 0x03;
        private const float MaxToolHapticFrequencyHz = 60f;
        private const uint DefaultImpactSeed = 0x9E3779B9u;

        [Header("Short Circuits")]
        [SerializeField, Range(0f, 1f), Tooltip("Impact severity converted to random short-circuit bit toggles.")]
        private float defaultImpactSeverity01 = 0.65f;

        [SerializeField, Min(0.01f), Tooltip("Seconds each active spark draw remains visible after impact.")]
        private float sparkLifetimeSeconds = 4.5f;

        [SerializeField, Min(0.01f), Tooltip("Spark quad phase rate. Higher values produce faster deterministic flicker.")]
        private float sparkFlickerRateHz = 24f;

        [SerializeField, Min(0.001f), Tooltip("Minimum spark quad scale.")]
        private float minimumSparkScale = 0.035f;

        [SerializeField, Min(0.001f), Tooltip("Maximum spark quad scale.")]
        private float maximumSparkScale = 0.12f;

        [Header("Rendering")]
        [SerializeField, Tooltip("Spark quad mesh drawn with Graphics.DrawMeshInstanced.")]
        private Mesh sparkQuadMesh;

        [SerializeField, Tooltip("Spark material. The shader must be GPU-instancing compatible.")]
        private Material sparkMaterial;

        [SerializeField, Tooltip("Optional per-bit anchor transforms. Missing anchors fall back to this component transform.")]
        private Transform[] sparkAnchors;

        [SerializeField, Tooltip("Layer used by spark draw calls.")]
        private int renderLayer;

        [Header("Haptics")]
        [SerializeField, Tooltip("Emit haptic pulses when short-circuit bits toggle.")]
        private bool hapticsEnabled = true;

        [SerializeField, Range(0f, 1f), Tooltip("Low-frequency short-circuit haptic pulse.")]
        private float shortCircuitLowFrequency = 0.1f;

        [SerializeField, Range(0f, 1f), Tooltip("High-frequency short-circuit haptic pulse.")]
        private float shortCircuitHighFrequency = 0.5f;

        [SerializeField, Min(0.01f), Tooltip("Short-circuit haptic duration.")]
        private float shortCircuitHapticDurationSeconds = 0.045f;

        [SerializeField, Min(1f), Tooltip("Short-circuit haptic frequency.")]
        private float shortCircuitHapticFrequencyHz = 142f;

        private Matrix4x4[] _sparkMatrices;
        private Vector3[] _sparkAnchorPositions;
        private Quaternion[] _sparkAnchorRotations;
        private ushort _shortCircuitMask;
        private ushort _sparkAnchorValidMask;
        private uint _rngState = DefaultImpactSeed;
        private float _sparkTimerSeconds;
        private float _sparkPhase;
        private float _resolvedDefaultImpactSeverity01;
        private float _resolvedSparkLifetimeSeconds;
        private float _resolvedSparkFlickerRateHz;
        private float _resolvedMinimumSparkScale;
        private float _resolvedMaximumSparkScale;
        private float _resolvedShortCircuitLowFrequency;
        private float _resolvedShortCircuitHighFrequency;
        private float _resolvedShortCircuitHapticDurationSeconds;
        private float _resolvedShortCircuitHapticFrequencyHz;
        private int _resolvedRenderLayer;
        private bool _registeredTick;

        /// <summary>
        /// Active short-circuit state. Each bit maps to one possible spark anchor.
        /// </summary>
        public ushort ShortCircuitMask => _shortCircuitMask;

        /// <summary>
        /// True when at least one short-circuit bit is active.
        /// </summary>
        public bool HasActiveShortCircuits => _shortCircuitMask != 0;

        private void Awake()
        {
            _sparkMatrices = new Matrix4x4[MaxVisibleSparkInstances]; // COLD ALLOC: Matrix4x4[4] — capped instanced spark draw buffer — owner: LifePodDamageSystem
            _sparkAnchorPositions = new Vector3[MaxShortCircuitBits]; // COLD ALLOC: Vector3[16] — cached panel spark positions sampled only on impact/state change — owner: LifePodDamageSystem
            _sparkAnchorRotations = new Quaternion[MaxShortCircuitBits]; // COLD ALLOC: Quaternion[16] — cached panel spark rotations sampled only on impact/state change — owner: LifePodDamageSystem
            CacheScalarConfig();
            if (renderLayer == 0)
                renderLayer = gameObject.layer;
        }

        private void OnEnable()
        {
            CacheScalarConfig();
            if (_shortCircuitMask != 0)
            {
                CacheSparkAnchorPoses(_shortCircuitMask);
                TryRegisterTick();
            }
        }

        private void OnDisable()
        {
            TryUnregisterTick();
        }

        /// <summary>
        /// Triggers randomized LifePod short circuits using the configured default severity.
        /// </summary>
        public void TriggerWaterImpact()
        {
            CacheScalarConfig();
            TriggerWaterImpactCachedConfig(DefaultImpactSeed, _resolvedDefaultImpactSeverity01);
        }

        /// <summary>
        /// Triggers randomized LifePod short circuits from an external crash seed.
        /// </summary>
        public void TriggerWaterImpact(uint impactSeed, float severity01)
        {
            CacheScalarConfig();
            TriggerWaterImpactCachedConfig(impactSeed, severity01);
        }

        private void TriggerWaterImpactCachedConfig(uint impactSeed, float severity01)
        {
            _rngState = impactSeed != 0u ? impactSeed : DefaultImpactSeed;
            float clampedSeverity = SaturateFinite01(severity01);
            if (clampedSeverity <= 0f)
                return;

            int toggleCount = math.clamp((int)math.ceil(clampedSeverity * MaxShortCircuitBits), 1, MaxShortCircuitBits);
            ushort toggleMask = 0;
            int selectedCount = 0;
            int attempts = 0;

            while (selectedCount < toggleCount && attempts < MaxShortCircuitBits * 4)
            {
                int bitIndex = (int)(NextRandom() & 0x0Fu);
                ushort bit = (ushort)(1 << bitIndex);
                if ((toggleMask & bit) == 0)
                {
                    toggleMask |= bit;
                    selectedCount++;
                }

                attempts++;
            }

            for (int bitIndex = 0; selectedCount < toggleCount && bitIndex < MaxShortCircuitBits; bitIndex++)
            {
                ushort bit = (ushort)(1 << bitIndex);
                if ((toggleMask & bit) != 0)
                    continue;

                toggleMask |= bit;
                selectedCount++;
            }

            if (toggleMask == 0)
                return;

            _shortCircuitMask = (ushort)(_shortCircuitMask ^ toggleMask);
            _sparkAnchorValidMask = 0;
            CacheSparkAnchorPoses(_shortCircuitMask);
            QueueShortCircuitHaptic();
            _sparkTimerSeconds = _resolvedSparkLifetimeSeconds;
            if (_shortCircuitMask != 0)
            {
                TryRegisterTick();
            }
            else
            {
                _sparkAnchorValidMask = 0;
                TryUnregisterTick();
            }
        }

        /// <summary>
        /// Sets one short-circuit bit explicitly.
        /// </summary>
        public void SetShortCircuitBit(int bitIndex, bool active)
        {
            if ((uint)bitIndex >= MaxShortCircuitBits)
                return;

            CacheScalarConfig();
            ushort bit = (ushort)(1 << bitIndex);
            bool wasActive = (_shortCircuitMask & bit) != 0;
            if (wasActive == active)
                return;

            if (active)
            {
                _shortCircuitMask |= bit;
                CacheSparkAnchorPose(bitIndex);
            }
            else
            {
                _shortCircuitMask = (ushort)(_shortCircuitMask & ~bit);
                _sparkAnchorValidMask = (ushort)(_sparkAnchorValidMask & ~bit);
                if (_shortCircuitMask == 0)
                    _sparkTimerSeconds = 0f;
            }

            QueueShortCircuitHaptic();
            if (_shortCircuitMask != 0)
            {
                _sparkTimerSeconds = math.max(_sparkTimerSeconds, _resolvedSparkLifetimeSeconds);
                TryRegisterTick();
            }
            else
            {
                TryUnregisterTick();
            }
        }

        /// <summary>
        /// Clears all active short-circuit bits.
        /// </summary>
        public void ClearShortCircuits()
        {
            _shortCircuitMask = 0;
            _sparkAnchorValidMask = 0;
            _sparkTimerSeconds = 0f;
            TryUnregisterTick();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_shortCircuitMask == 0)
            {
                TryUnregisterTick();
                return;
            }

            float safeDeltaTime = SanitizeAtLeast(deltaTime, 0f);
            _sparkTimerSeconds = math.max(0f, _sparkTimerSeconds - safeDeltaTime);
            if (_sparkTimerSeconds <= 0f)
            {
                ClearShortCircuits();
                return;
            }

            _sparkPhase = math.frac(_sparkPhase + safeDeltaTime * _resolvedSparkFlickerRateHz);
            DrawActiveSparks();
        }

        private void DrawActiveSparks()
        {
            if (sparkQuadMesh == null ||
                sparkMaterial == null ||
                !sparkMaterial.enableInstancing ||
                _sparkMatrices == null)
            {
                return;
            }

            int activeCount = 0;
            uint activeMask = _shortCircuitMask;
            while (activeMask != 0u && activeCount < MaxVisibleSparkInstances)
            {
                int bitIndex = math.tzcnt(activeMask);
                activeMask &= activeMask - 1u;

                if (!TryResolveCachedSparkPose(bitIndex, out Vector3 position, out Quaternion rotation))
                    continue;

                float flicker01 = ResolveSparkFlicker01(bitIndex);
                float scale = math.lerp(_resolvedMinimumSparkScale, _resolvedMaximumSparkScale, flicker01);
                _sparkMatrices[activeCount] = Matrix4x4.TRS(
                    position,
                    rotation,
                    new Vector3(scale, scale, scale));
                activeCount++;
            }

            if (activeCount <= 0)
                return;

            UnityEngine.Graphics.DrawMeshInstanced(
                sparkQuadMesh,
                0,
                sparkMaterial,
                _sparkMatrices,
                activeCount,
                null,
                ShadowCastingMode.Off,
                false,
                _resolvedRenderLayer,
                null,
                LightProbeUsage.Off,
                null);
        }

        private Transform ResolveSparkAnchor(int bitIndex)
        {
            if (sparkAnchors != null && bitIndex < sparkAnchors.Length && sparkAnchors[bitIndex] != null)
                return sparkAnchors[bitIndex];

            return transform;
        }

        private void CacheSparkAnchorPoses(ushort mask)
        {
            uint activeMask = mask;
            while (activeMask != 0u)
            {
                int bitIndex = math.tzcnt(activeMask);
                activeMask &= activeMask - 1u;
                CacheSparkAnchorPose(bitIndex);
            }
        }

        private bool CacheSparkAnchorPose(int bitIndex)
        {
            if ((uint)bitIndex >= MaxShortCircuitBits ||
                _sparkAnchorPositions == null ||
                _sparkAnchorRotations == null)
            {
                return false;
            }

            ushort bit = (ushort)(1 << bitIndex);
            _sparkAnchorValidMask = (ushort)(_sparkAnchorValidMask & ~bit);
            Transform anchor = ResolveSparkAnchor(bitIndex);
            if (anchor == null)
                return false;

            Vector3 position = anchor.position;
            Quaternion rotation = anchor.rotation;
            if (!IsFinite(position) || !IsFinite(rotation))
                return false;

            _sparkAnchorPositions[bitIndex] = position;
            _sparkAnchorRotations[bitIndex] = rotation;
            _sparkAnchorValidMask = (ushort)(_sparkAnchorValidMask | bit);
            return true;
        }

        private bool TryResolveCachedSparkPose(int bitIndex, out Vector3 position, out Quaternion rotation)
        {
            ushort bit = (ushort)(1 << bitIndex);
            if ((_sparkAnchorValidMask & bit) != 0)
            {
                position = _sparkAnchorPositions[bitIndex];
                rotation = _sparkAnchorRotations[bitIndex];
                return true;
            }

            position = default;
            rotation = default;
            return false;
        }

        private float ResolveSparkFlicker01(int bitIndex)
        {
            float phase01 = math.frac(_sparkPhase + bitIndex * 0.618f);
            float triangle01 = 1f - math.abs((phase01 * 2f) - 1f);
            return math.saturate(triangle01);
        }

        private uint NextRandom()
        {
            uint x = _rngState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rngState = x != 0u ? x : DefaultImpactSeed;
            return _rngState;
        }

        private void QueueShortCircuitHaptic()
        {
            if (!hapticsEnabled)
                return;

            ToolHapticsRuntime.EnqueueSinusoidalCommand(
                _resolvedShortCircuitLowFrequency,
                _resolvedShortCircuitHighFrequency,
                _resolvedShortCircuitHapticDurationSeconds,
                _resolvedShortCircuitHapticFrequencyHz,
                HapticPriorityCritical,
                BothMotorMask);
        }

        private void CacheScalarConfig()
        {
            _resolvedDefaultImpactSeverity01 = SaturateFinite01(defaultImpactSeverity01);
            _resolvedSparkLifetimeSeconds = SanitizeAtLeast(sparkLifetimeSeconds, 0.01f);
            _resolvedSparkFlickerRateHz = SanitizeAtLeast(sparkFlickerRateHz, 0.01f);
            _resolvedMinimumSparkScale = SanitizeAtLeast(minimumSparkScale, 0.001f);
            _resolvedMaximumSparkScale = SanitizeAtLeast(maximumSparkScale, _resolvedMinimumSparkScale);
            if (_resolvedMaximumSparkScale < _resolvedMinimumSparkScale)
                _resolvedMaximumSparkScale = _resolvedMinimumSparkScale;

            _resolvedShortCircuitLowFrequency = SaturateFinite01(shortCircuitLowFrequency);
            _resolvedShortCircuitHighFrequency = SaturateFinite01(shortCircuitHighFrequency);
            _resolvedShortCircuitHapticDurationSeconds = SanitizeAtLeast(shortCircuitHapticDurationSeconds, 0.01f);
            _resolvedShortCircuitHapticFrequencyHz = math.clamp(
                SanitizeAtLeast(shortCircuitHapticFrequencyHz, 1f),
                1f,
                MaxToolHapticFrequencyHz);
            _resolvedRenderLayer = ClampRenderLayer(renderLayer != 0 ? renderLayer : gameObject.layer);
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

        private static int ClampRenderLayer(int value)
        {
            return math.clamp(value, 0, MaximumRenderLayer);
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool IsFinite(Quaternion value)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            return math.all(math.isfinite(q)) && math.lengthsq(q) > 0.000001f;
        }

        private void TryRegisterTick()
        {
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredTick = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheScalarConfig();
        }
#endif
    }
}
