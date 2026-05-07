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
        private const byte HapticPriorityCritical = 3;
        private const byte BothMotorMask = 0x03;
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

        private Matrix4x4[] _sparkMatrices; // COLD ALLOC: Matrix4x4[16] - instanced spark draw buffer - owner: LifePodDamageSystem
        private ushort _shortCircuitMask;
        private uint _rngState = DefaultImpactSeed;
        private float _sparkTimerSeconds;
        private float _sparkPhase;
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
            _sparkMatrices = new Matrix4x4[MaxShortCircuitBits];
            if (renderLayer == 0)
                renderLayer = gameObject.layer;
        }

        private void OnEnable()
        {
            if (_shortCircuitMask != 0)
                TryRegisterTick();
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
            TriggerWaterImpact(DefaultImpactSeed, defaultImpactSeverity01);
        }

        /// <summary>
        /// Triggers randomized LifePod short circuits from an external crash seed.
        /// </summary>
        public void TriggerWaterImpact(uint impactSeed, float severity01)
        {
            _rngState = impactSeed != 0u ? impactSeed : DefaultImpactSeed;
            float clampedSeverity = math.saturate(severity01);
            int toggleCount = math.clamp((int)math.ceil(clampedSeverity * MaxShortCircuitBits), 1, MaxShortCircuitBits);

            for (int i = 0; i < toggleCount; i++)
            {
                int bitIndex = (int)(NextRandom() & 0x0Fu);
                ToggleShortCircuitBit(bitIndex);
            }

            _sparkTimerSeconds = sparkLifetimeSeconds;
            TryRegisterTick();
        }

        /// <summary>
        /// Sets one short-circuit bit explicitly.
        /// </summary>
        public void SetShortCircuitBit(int bitIndex, bool active)
        {
            if ((uint)bitIndex >= MaxShortCircuitBits)
                return;

            ushort bit = (ushort)(1 << bitIndex);
            bool wasActive = (_shortCircuitMask & bit) != 0;
            if (wasActive == active)
                return;

            if (active)
                _shortCircuitMask |= bit;
            else
                _shortCircuitMask = (ushort)(_shortCircuitMask & ~bit);

            QueueShortCircuitHaptic();
            if (_shortCircuitMask != 0)
            {
                _sparkTimerSeconds = sparkLifetimeSeconds;
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

            float safeDeltaTime = math.max(0f, deltaTime);
            _sparkTimerSeconds = math.max(0f, _sparkTimerSeconds - safeDeltaTime);
            if (_sparkTimerSeconds <= 0f)
            {
                ClearShortCircuits();
                return;
            }

            _sparkPhase += safeDeltaTime * sparkFlickerRateHz;
            DrawActiveSparks();
        }

        private void ToggleShortCircuitBit(int bitIndex)
        {
            ushort bit = (ushort)(1 << bitIndex);
            _shortCircuitMask = (ushort)(_shortCircuitMask ^ bit);
            QueueShortCircuitHaptic();
        }

        private void DrawActiveSparks()
        {
            if (sparkQuadMesh == null || sparkMaterial == null || _sparkMatrices == null)
                return;

            int activeCount = 0;
            for (int bitIndex = 0; bitIndex < MaxShortCircuitBits; bitIndex++)
            {
                ushort bit = (ushort)(1 << bitIndex);
                if ((_shortCircuitMask & bit) == 0)
                    continue;

                Transform anchor = ResolveSparkAnchor(bitIndex);
                if (anchor == null)
                    continue;

                float flicker01 = ResolveSparkFlicker01(bitIndex);
                float scale = math.lerp(minimumSparkScale, maximumSparkScale, flicker01);
                _sparkMatrices[activeCount] = Matrix4x4.TRS(
                    anchor.position,
                    anchor.rotation,
                    new Vector3(scale, scale, scale));
                activeCount++;
            }

            if (activeCount <= 0)
                return;

            Graphics.DrawMeshInstanced(
                sparkQuadMesh,
                0,
                sparkMaterial,
                _sparkMatrices,
                activeCount,
                null,
                ShadowCastingMode.Off,
                false,
                renderLayer,
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

        private float ResolveSparkFlicker01(int bitIndex)
        {
            float phase01 = math.frac(_sparkPhase + bitIndex * 0.61803395f);
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
                shortCircuitLowFrequency,
                shortCircuitHighFrequency,
                shortCircuitHapticDurationSeconds,
                shortCircuitHapticFrequencyHz,
                HapticPriorityCritical,
                BothMotorMask);
        }

        private void TryRegisterTick()
        {
            if (_registeredTick || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredTick = true;
        }

        private void TryUnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredTick = false;
        }
    }
}
