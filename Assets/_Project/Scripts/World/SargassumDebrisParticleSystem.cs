using Hecton8.Bootstrap;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Emits world-space burst debris for cut events and ambient suspended bloom particles inside dense sargassum.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SargassumDebrisParticleSystem : MonoBehaviour, ITickable
    {
        private static readonly int _DryColorId = Shader.PropertyToID("_DryColor");
        private static readonly int _WetColorId = Shader.PropertyToID("_WetColor");
        private static readonly int _BubbleColorId = Shader.PropertyToID("_BubbleColor");

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Optional explicit ParticleSystem override. Falls back to the component on the same GameObject.")]
        private ParticleSystem particleSystemOverride;

        [SerializeField]
        [Tooltip("Optional explicit ParticleSystemRenderer override used to assign the debris material.")]
        private ParticleSystemRenderer particleSystemRendererOverride;

        [SerializeField]
        [Tooltip("Optional player override used when bootstrap has not resolved the runtime player yet.")]
        private Transform playerTransformOverride;

        [SerializeField]
        [Tooltip("Sargassum source material used to pull the leaf and bladder palette.")]
        private Material paletteSourceMaterial;

        [SerializeField]
        [Tooltip("Shared particle material used by the debris system.")]
        private Material debrisMaterial;

        [Header("── Burst Emission ─────────────────")]
        [SerializeField, Min(1)]
        [Tooltip("Minimum number of leaf particles emitted per cut stamp.")]
        private int minLeafParticles = 4;

        [SerializeField, Min(1)]
        [Tooltip("Maximum number of leaf particles emitted per cut stamp.")]
        private int maxLeafParticles = 14;

        [SerializeField, Min(0)]
        [Tooltip("Maximum number of bladder particles emitted per cut stamp.")]
        private int maxBubbleParticles = 4;

        [SerializeField, Range(0.05f, 4f)]
        [Tooltip("Base leaf particle lifetime.")]
        private float leafLifetime = 1.15f;

        [SerializeField, Range(0.05f, 4f)]
        [Tooltip("Base bladder particle lifetime.")]
        private float bubbleLifetime = 0.72f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Base leaf particle size.")]
        private float leafSize = 0.18f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Base bladder particle size.")]
        private float bubbleSize = 0.11f;

        [SerializeField, Range(0f, 6f)]
        [Tooltip("Base launch speed of cut debris.")]
        private float burstSpeed = 1.85f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Upward lift applied to the debris burst.")]
        private float upwardLift = 0.55f;

        [Header("── Ambient Bloom ─────────────────")]
        [SerializeField]
        [Tooltip("Enables the low-velocity suspended debris soup around dense sargassum clusters.")]
        private bool enableAmbientBloom = true;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum sampled sargassum density required before ambient bloom particles begin spawning.")]
        private float ambientDensityThreshold = 0.32f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Radius used when sampling the global sargassum density field for ambient bloom particles.")]
        private float ambientSampleRadius = 2.6f;

        [SerializeField, Range(0f, 48f)]
        [Tooltip("Maximum ambient particle spawn rate at full local density.")]
        private float ambientSpawnRate = 18f;

        [SerializeField]
        [Tooltip("World-space volume around the player where ambient bloom particles are spawned.")]
        private Vector3 ambientVolume = new Vector3(8f, 4f, 8f);

        [SerializeField]
        [Tooltip("Offset from the player origin used as the center of the ambient bloom volume.")]
        private Vector3 ambientOffset = new Vector3(0f, 0.45f, 0f);

        [SerializeField, Range(0.05f, 8f)]
        [Tooltip("Base lifetime of ambient bloom particles.")]
        private float ambientLifetime = 2.4f;

        [SerializeField, Range(0.01f, 0.25f)]
        [Tooltip("Base size of ambient bloom particles.")]
        private float ambientSize = 0.055f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Chance that an ambient particle is an amber bladder fragment instead of a leaf scrap.")]
        private float ambientBubbleChance = 0.22f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Lateral drift speed of ambient bloom particles.")]
        private float ambientDriftSpeed = 0.18f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Vertical rise speed of ambient bloom particles.")]
        private float ambientRiseSpeed = 0.08f;

        [Header("── Palette ───────────────────────")]
        [SerializeField]
        [Tooltip("Fallback leaf shadow color used if the source material is missing required properties.")]
        private Color fallbackLeafShadowColor = new Color(0.34f, 0.25f, 0.10f, 1f);

        [SerializeField]
        [Tooltip("Fallback leaf highlight color used if the source material is missing required properties.")]
        private Color fallbackLeafHighlightColor = new Color(0.60f, 0.42f, 0.18f, 1f);

        [SerializeField]
        [Tooltip("Fallback bladder color used if the source material is missing required properties.")]
        private Color fallbackBubbleColor = new Color(1.00f, 0.78f, 0.34f, 1f);

        [Header("── Diagnostics ───────────────────")]
        [SerializeField]
        [Tooltip("Current sampled sargassum density around the ambient bloom volume.")]
        private float _debugAmbientDensity01;

        [SerializeField]
        [Tooltip("Current sub-particle ambient spawn budget carried into the next tick.")]
        private float _debugAmbientSpawnBudget;

        [SerializeField]
        [Tooltip("Number of ambient particles emitted during the latest tick.")]
        private int _debugAmbientEmissionThisTick;

        private ParticleSystem _particleSystem;
        private ParticleSystemRenderer _particleRenderer;
        private Transform _playerTransform;
        private Color _leafShadowColor;
        private Color _leafHighlightColor;
        private Color _bubbleColor;
        private float _ambientSpawnAccumulator;
        private uint _emitSeed = 1u;
        private bool _registered;

        private void Awake()
        {
            ResolveDependencies();
            ResolveRuntimeTargets();
            RefreshPaletteFromMaterial();
            ApplyRendererMaterial();
            ConfigureParticleSystem();
        }

        private void OnEnable()
        {
            ResolveRuntimeTargets();
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            _ambientSpawnAccumulator = 0f;
            _debugAmbientDensity01 = 0f;
            _debugAmbientSpawnBudget = 0f;
            _debugAmbientEmissionThisTick = 0;
        }

        private void OnValidate()
        {
            minLeafParticles = Mathf.Max(1, minLeafParticles);
            maxLeafParticles = Mathf.Max(minLeafParticles, maxLeafParticles);
            maxBubbleParticles = Mathf.Max(0, maxBubbleParticles);
            leafLifetime = Mathf.Max(0.05f, leafLifetime);
            bubbleLifetime = Mathf.Max(0.05f, bubbleLifetime);
            leafSize = Mathf.Max(0.01f, leafSize);
            bubbleSize = Mathf.Max(0.01f, bubbleSize);
            ambientDensityThreshold = Mathf.Clamp01(ambientDensityThreshold);
            ambientSampleRadius = Mathf.Max(0.1f, ambientSampleRadius);
            ambientSpawnRate = Mathf.Max(0f, ambientSpawnRate);
            ambientVolume.x = Mathf.Max(0.1f, ambientVolume.x);
            ambientVolume.y = Mathf.Max(0.1f, ambientVolume.y);
            ambientVolume.z = Mathf.Max(0.1f, ambientVolume.z);
            ambientLifetime = Mathf.Max(0.05f, ambientLifetime);
            ambientSize = Mathf.Max(0.01f, ambientSize);
            ambientBubbleChance = Mathf.Clamp01(ambientBubbleChance);
            ambientDriftSpeed = Mathf.Max(0f, ambientDriftSpeed);
            ambientRiseSpeed = Mathf.Max(0f, ambientRiseSpeed);
        }

        /// <summary>
        /// Refreshes the local debris palette from the assigned source material.
        /// </summary>
        public void RefreshPaletteFromMaterial()
        {
            _leafShadowColor = fallbackLeafShadowColor;
            _leafHighlightColor = fallbackLeafHighlightColor;
            _bubbleColor = fallbackBubbleColor;

            if (paletteSourceMaterial == null)
                return;

            if (paletteSourceMaterial.HasProperty(_WetColorId))
                _leafShadowColor = paletteSourceMaterial.GetColor(_WetColorId);

            if (paletteSourceMaterial.HasProperty(_DryColorId))
                _leafHighlightColor = paletteSourceMaterial.GetColor(_DryColorId);

            if (paletteSourceMaterial.HasProperty(_BubbleColorId))
                _bubbleColor = paletteSourceMaterial.GetColor(_BubbleColorId);
        }

        /// <summary>
        /// Emits a debris burst at the provided world-space cut point.
        /// </summary>
        /// <param name="positionWS">World-space cut position.</param>
        /// <param name="directionWS">Preferred burst direction.</param>
        /// <param name="strength01">Normalized cut intensity in the 0..1 range.</param>
        /// <param name="bubbleWeight">Normalized bladder-fragment intensity in the 0..1 range.</param>
        public void EmitBurst(Vector3 positionWS, Vector3 directionWS, float strength01, float bubbleWeight)
        {
            if (_particleSystem == null)
                return;

            float intensity = Mathf.Clamp01(strength01);
            if (intensity <= 0.0001f)
                return;

            Vector3 normalizedDirection = directionWS.sqrMagnitude > 0.0001f
                ? directionWS.normalized
                : Vector3.up;
            Vector3 baseVelocity = normalizedDirection * Mathf.Lerp(burstSpeed * 0.6f, burstSpeed, intensity);
            baseVelocity.y += upwardLift;

            int leafCount = Mathf.RoundToInt(Mathf.Lerp(minLeafParticles, maxLeafParticles, intensity));
            EmitGroup(
                positionWS,
                baseVelocity,
                leafCount,
                leafLifetime * Mathf.Lerp(0.82f, 1.12f, Next01()),
                leafSize * Mathf.Lerp(0.82f, 1.24f, intensity),
                Color.Lerp(_leafShadowColor, _leafHighlightColor, Next01()));

            int bubbleCount = Mathf.RoundToInt(maxBubbleParticles * Mathf.Clamp01(bubbleWeight));
            if (bubbleCount <= 0)
                return;

            Vector3 bubbleVelocity = baseVelocity * 0.55f;
            bubbleVelocity.y += upwardLift * 0.6f;
            EmitGroup(
                positionWS,
                bubbleVelocity,
                bubbleCount,
                bubbleLifetime * Mathf.Lerp(0.88f, 1.12f, Next01()),
                bubbleSize * Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(bubbleWeight)),
                _bubbleColor);
        }

        /// <summary>
        /// Emits ambient suspended debris around the player while they move through dense sargassum.
        /// </summary>
        /// <param name="deltaTime">Gameplay frame delta supplied by GameTickManager.</param>
        public void Tick(float deltaTime)
        {
            _debugAmbientEmissionThisTick = 0;

            if (!enableAmbientBloom || _particleSystem == null)
                return;

            ResolveRuntimeTargets();
            if (_playerTransform == null)
            {
                _debugAmbientDensity01 = 0f;
                _debugAmbientSpawnBudget = _ambientSpawnAccumulator;
                return;
            }

            SargassumGlobalDragManager dragManager = Hecton8.Core.GlobalRegistry.SargassumDrag;
            if (dragManager == null || !dragManager.HasFieldData)
            {
                _debugAmbientDensity01 = 0f;
                _ambientSpawnAccumulator = Mathf.Min(_ambientSpawnAccumulator, 1f);
                _debugAmbientSpawnBudget = _ambientSpawnAccumulator;
                return;
            }

            Vector3 sampleCenterWS = _playerTransform.position + ambientOffset;
            bool hasSample = dragManager.SampleDetailedInfluence(
                sampleCenterWS,
                ambientSampleRadius,
                0f,
                out SargassumGlobalDragManager.SargassumFieldSample sample);

            if (!hasSample)
            {
                _debugAmbientDensity01 = 0f;
                _ambientSpawnAccumulator = Mathf.Min(_ambientSpawnAccumulator, 1f);
                _debugAmbientSpawnBudget = _ambientSpawnAccumulator;
                return;
            }

            _debugAmbientDensity01 = sample.Density01;
            float densityT = Mathf.InverseLerp(ambientDensityThreshold, 1f, sample.Density01);
            if (densityT <= 0f)
            {
                _ambientSpawnAccumulator = Mathf.Min(_ambientSpawnAccumulator, 1f);
                _debugAmbientSpawnBudget = _ambientSpawnAccumulator;
                return;
            }

            float canopyDensityBias = Mathf.Lerp(0.82f, 1.12f, 1f - sample.Window01);
            _ambientSpawnAccumulator = Mathf.Min(
                _ambientSpawnAccumulator + ambientSpawnRate * densityT * canopyDensityBias * deltaTime,
                8f);

            int spawnCount = Mathf.Min(6, Mathf.FloorToInt(_ambientSpawnAccumulator));
            if (spawnCount > 0)
                _ambientSpawnAccumulator -= spawnCount;

            for (int i = 0; i < spawnCount; i++)
            {
                EmitAmbientParticle(sampleCenterWS, densityT, sample.Window01);
                _debugAmbientEmissionThisTick++;
            }

            _debugAmbientSpawnBudget = _ambientSpawnAccumulator;
        }

        private void ResolveDependencies()
        {
            if (particleSystemOverride == null)
                particleSystemOverride = GetComponent<ParticleSystem>();

            _particleSystem = particleSystemOverride;
            if (_particleSystem == null)
                return;

            if (particleSystemRendererOverride == null)
                particleSystemRendererOverride = _particleSystem.GetComponent<ParticleSystemRenderer>();

            _particleRenderer = particleSystemRendererOverride;
        }

        private void ResolveRuntimeTargets()
        {
            Transform runtimePlayerTransform = BootstrapState.CurrentPlayerTransform;
            _playerTransform = runtimePlayerTransform != null ? runtimePlayerTransform : playerTransformOverride;
        }

        private void ConfigureParticleSystem()
        {
            if (_particleSystem == null)
                return;

            ParticleSystem.MainModule main = _particleSystem.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }

        private void ApplyRendererMaterial()
        {
            if (_particleRenderer == null || debrisMaterial == null)
                return;

            _particleRenderer.sharedMaterial = debrisMaterial;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void EmitAmbientParticle(Vector3 centerWS, float densityT, float window01)
        {
            bool emitBubble = Next01() < ambientBubbleChance * Mathf.Lerp(0.9f, 1.15f, window01);
            float size = emitBubble ? bubbleSize * 0.42f : ambientSize;
            float lifetime = emitBubble ? bubbleLifetime * 1.65f : ambientLifetime;
            Color color = emitBubble
                ? _bubbleColor
                : Color.Lerp(_leafShadowColor, _leafHighlightColor, Next01() * 0.75f);

            EmitSingle(
                BuildAmbientPosition(centerWS),
                BuildAmbientVelocity(densityT),
                lifetime * Mathf.Lerp(0.82f, 1.18f, Next01()),
                size * Mathf.Lerp(0.78f, 1.22f, densityT),
                color);
        }

        private Vector3 BuildAmbientPosition(Vector3 centerWS)
        {
            Vector3 halfExtents = ambientVolume * 0.5f;
            return new Vector3(
                centerWS.x + NextSigned(halfExtents.x),
                centerWS.y + NextSigned(halfExtents.y),
                centerWS.z + NextSigned(halfExtents.z));
        }

        private Vector3 BuildAmbientVelocity(float densityT)
        {
            float lateralScale = ambientDriftSpeed * Mathf.Lerp(0.7f, 1.15f, densityT);
            return new Vector3(
                NextSigned(lateralScale),
                ambientRiseSpeed * Mathf.Lerp(0.8f, 1.2f, Next01()),
                NextSigned(lateralScale));
        }

        private void EmitGroup(Vector3 positionWS, Vector3 velocityWS, int count, float lifetime, float size, Color color)
        {
            if (count <= 0)
                return;

            ParticleSystem.EmitParams emitParams = default;
            emitParams.position = positionWS;
            emitParams.velocity = velocityWS + BuildJitterVector();
            emitParams.startLifetime = lifetime;
            emitParams.startSize = size;
            emitParams.startColor = color;
            emitParams.randomSeed = NextSeed();
            _particleSystem.Emit(emitParams, count);
        }

        private void EmitSingle(Vector3 positionWS, Vector3 velocityWS, float lifetime, float size, Color color)
        {
            ParticleSystem.EmitParams emitParams = default;
            emitParams.position = positionWS;
            emitParams.velocity = velocityWS;
            emitParams.startLifetime = lifetime;
            emitParams.startSize = size;
            emitParams.startColor = color;
            emitParams.randomSeed = NextSeed();
            _particleSystem.Emit(emitParams, 1);
        }

        private Vector3 BuildJitterVector()
        {
            float jitterX = Mathf.Lerp(-0.65f, 0.65f, Next01());
            float jitterY = Mathf.Lerp(0.05f, 0.85f, Next01());
            float jitterZ = Mathf.Lerp(-0.65f, 0.65f, Next01());
            return new Vector3(jitterX, jitterY, jitterZ);
        }

        private float NextSigned(float magnitude)
        {
            return ((Next01() * 2f) - 1f) * magnitude;
        }

        private uint NextSeed()
        {
            _emitSeed = _emitSeed * 1664525u + 1013904223u;
            return _emitSeed;
        }

        private float Next01()
        {
            return (NextSeed() & 0x00FFFFFFu) * (1.0f / 16777215.0f);
        }
    }
}
