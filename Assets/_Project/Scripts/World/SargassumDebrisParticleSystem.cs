using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.VFX;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Emits world-space burst debris for cut events and ambient suspended bloom particles inside dense sargassum.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SargassumDebrisParticleSystem : MonoBehaviour, ILateFrameTickable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxQueuedParticleEmits = 64;
        private const int MaxParticlesPerQueuedEmit = 32;
        private const int MaxLeafParticlesPerBurst = 32;
        private const int MaxBubbleParticlesPerBurst = 16;
        private const float MaxAmbientSpawnRate = 48f;
        private const float QualityRefreshEpsilon = 0.01f;
        private const float MinimumRendererMaxParticleSize = 0.025f;
        private const float OverkillRendererMaxParticleSize = 0.075f;
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
        private bool _lateFrameRegistered;
        private bool _slowTickRegistered;
        private bool _hotSwapRegistered;
        private bool _runtimeTargetRefreshRequested = true;
        private int _queuedEmitCount;
        private int _queuedParticleCount;
        private int _appliedQualityParticleCap;
        private float _appliedQualityWeight = -1f;
        private SargassumGlobalDragManager _sargassumDrag;
        private readonly PendingParticleEmit[] _queuedEmits = new PendingParticleEmit[MaxQueuedParticleEmits]; // COLD ALLOC: PendingParticleEmit[64] - fixed debris emit queue for LateFrameTick flushing - owner: SargassumDebrisParticleSystem

        private void Awake()
        {
            ResolveDependencies();
            CacheRegistryServicesCold();
            ResolveRuntimeTargets();
            RefreshPaletteFromMaterial();
            ApplyRendererMaterial();
            ConfigureParticleSystem();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            ResolveRuntimeTargets();
            TryRegister();
        }

        private void Start()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            _runtimeTargetRefreshRequested = true;
            _ambientSpawnAccumulator = 0f;
            _debugAmbientDensity01 = 0f;
            _debugAmbientSpawnBudget = 0f;
            _debugAmbientEmissionThisTick = 0;
            _queuedEmitCount = 0;
            _queuedParticleCount = 0;
            _appliedQualityParticleCap = 0;
            _appliedQualityWeight = -1f;
            _sargassumDrag = null;
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            _sargassumDrag = null;
        }

        private void OnValidate()
        {
            minLeafParticles = Mathf.Max(1, minLeafParticles);
            minLeafParticles = Mathf.Min(minLeafParticles, MaxLeafParticlesPerBurst);
            maxLeafParticles = Mathf.Clamp(maxLeafParticles, minLeafParticles, MaxLeafParticlesPerBurst);
            maxBubbleParticles = Mathf.Clamp(maxBubbleParticles, 0, MaxBubbleParticlesPerBurst);
            leafLifetime = Mathf.Max(0.05f, leafLifetime);
            bubbleLifetime = Mathf.Max(0.05f, bubbleLifetime);
            leafSize = Mathf.Max(0.01f, leafSize);
            bubbleSize = Mathf.Max(0.01f, bubbleSize);
            ambientDensityThreshold = Mathf.Clamp01(ambientDensityThreshold);
            ambientSampleRadius = Mathf.Max(0.1f, ambientSampleRadius);
            ambientSpawnRate = Mathf.Clamp(ambientSpawnRate, 0f, MaxAmbientSpawnRate);
            ambientVolume.x = Mathf.Max(0.1f, ambientVolume.x);
            ambientVolume.y = Mathf.Max(0.1f, ambientVolume.y);
            ambientVolume.z = Mathf.Max(0.1f, ambientVolume.z);
            ambientLifetime = Mathf.Max(0.05f, ambientLifetime);
            ambientSize = Mathf.Max(0.01f, ambientSize);
            ambientBubbleChance = Mathf.Clamp01(ambientBubbleChance);
            ambientDriftSpeed = Mathf.Max(0f, ambientDriftSpeed);
            ambientRiseSpeed = Mathf.Max(0f, ambientRiseSpeed);
            _appliedQualityParticleCap = 0;
            _appliedQualityWeight = -1f;
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

            Vector3 normalizedDirection = ResolveSafeDirection(directionWS, Vector3.up);
            Vector3 baseVelocity = normalizedDirection * LerpClamped(burstSpeed * 0.6f, burstSpeed, intensity);
            baseVelocity.y += upwardLift;

            int leafCount = Mathf.RoundToInt(LerpClamped(minLeafParticles, maxLeafParticles, intensity));
            EmitGroup(
                positionWS,
                baseVelocity,
                leafCount,
                leafLifetime * LerpClamped(0.82f, 1.12f, Next01()),
                leafSize * LerpClamped(0.82f, 1.24f, intensity),
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
                bubbleLifetime * LerpClamped(0.88f, 1.12f, Next01()),
                bubbleSize * LerpClamped(0.85f, 1.15f, Mathf.Clamp01(bubbleWeight)),
                _bubbleColor);
        }

        /// <summary>
        /// Emits ambient suspended debris around the player while they move through dense sargassum.
        /// </summary>
        /// <param name="deltaTime">Gameplay frame delta supplied by GameTickManager.</param>
        private void AdvanceAmbientDebrisEmission(float deltaTime)
        {
            _debugAmbientEmissionThisTick = 0;

            if (!enableAmbientBloom || _particleSystem == null)
                return;

            if (_playerTransform == null)
            {
                QueueRuntimeTargetRefresh();
                _debugAmbientDensity01 = 0f;
                _debugAmbientSpawnBudget = _ambientSpawnAccumulator;
                return;
            }

            SargassumGlobalDragManager dragManager = _sargassumDrag;
            if (dragManager == null || !dragManager.HasFieldData)
            {
                _debugAmbientDensity01 = 0f;
                _ambientSpawnAccumulator = Mathf.Min(_ambientSpawnAccumulator, 1f);
                _debugAmbientSpawnBudget = _ambientSpawnAccumulator;
                return;
            }

            Vector3 sampleCenterWS = ResolvePlayerRuntimePosition() + ambientOffset;
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

            float canopyDensityBias = LerpClamped(0.82f, 1.12f, 1f - sample.Window01);
            _ambientSpawnAccumulator = Mathf.Min(
                _ambientSpawnAccumulator + ResolveAmbientSpawnRateForQuality() * densityT * canopyDensityBias * deltaTime,
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

        public void LateFrameTick()
        {
            RefreshQualityParticleCap();
            AdvanceAmbientDebrisEmission(SystemDispatcher.CurrentFrameDeltaTime);
            FlushQueuedParticleEmits();
        }

        public void SlowTick()
        {
            if (!_runtimeTargetRefreshRequested && _playerTransform != null)
                return;

            _runtimeTargetRefreshRequested = false;
            ResolveRuntimeTargets();
        }

        private Vector3 ResolvePlayerRuntimePosition()
        {
            return _playerTransform != null ? _playerTransform.position : Vector3.zero;
        }

        private void ResolveDependencies()
        {
            if (particleSystemOverride == null)
                TryGetComponent(out particleSystemOverride);

            _particleSystem = particleSystemOverride;
            if (_particleSystem == null)
                return;

            if (particleSystemRendererOverride == null)
                _particleSystem.TryGetComponent(out particleSystemRendererOverride);

            _particleRenderer = particleSystemRendererOverride;
        }

        private void ResolveRuntimeTargets()
        {
            Transform runtimePlayerTransform = BootstrapState.CurrentPlayerTransform;
            _playerTransform = runtimePlayerTransform != null ? runtimePlayerTransform : playerTransformOverride;
        }

        private void QueueRuntimeTargetRefresh()
        {
            _runtimeTargetRefreshRequested = true;
        }

        private void ConfigureParticleSystem()
        {
            if (_particleSystem == null)
                return;

            ParticleSystem.MainModule main = _particleSystem.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ApplyQualityParticleCap(ResolveGlobalQualityWeight01());
        }

        private void ApplyRendererMaterial()
        {
            if (_particleRenderer == null)
                return;

            if (debrisMaterial != null)
                _particleRenderer.sharedMaterial = debrisMaterial;

            _particleRenderer.maxParticleSize = ResolveRendererMaxParticleSize(ResolveGlobalQualityWeight01());
        }

        private void RefreshQualityParticleCap()
        {
            if (_particleSystem == null)
                return;

            float qualityWeight = ResolveGlobalQualityWeight01();
            if (_appliedQualityParticleCap > 0 &&
                math.abs(qualityWeight - _appliedQualityWeight) < QualityRefreshEpsilon)
            {
                return;
            }

            ApplyQualityParticleCap(qualityWeight);
        }

        private void ApplyQualityParticleCap(float qualityWeight)
        {
            if (_particleSystem == null)
                return;

            int particleCap = ResolveQualityParticleCap(qualityWeight);
            if (particleCap != _appliedQualityParticleCap)
            {
                ParticleSystem.MainModule main = _particleSystem.main;
                main.maxParticles = particleCap;
                _appliedQualityParticleCap = particleCap;
            }

            if (_particleRenderer != null)
                _particleRenderer.maxParticleSize = ResolveRendererMaxParticleSize(qualityWeight);

            _appliedQualityWeight = qualityWeight;
        }

        private int ResolveQualityParticleCap(float qualityWeight)
        {
            int catalogCap = VfxComputeParticleBudgetCatalog.ResolvePoolCapacity(
                qualityWeight,
                0,
                VFXEmissionProfile.FluidType.Debris);
            return math.clamp(
                math.max(1, catalogCap),
                1,
                VfxComputeParticleBudgetCatalog.OverkillQualityDebrisCount);
        }

        private static float ResolveRendererMaxParticleSize(float qualityWeight)
        {
            return LerpClamped(
                MinimumRendererMaxParticleSize,
                OverkillRendererMaxParticleSize,
                SmoothQuality01(qualityWeight));
        }

        private float ResolveAmbientSpawnRateForQuality()
        {
            float qualityWeight = _appliedQualityWeight >= 0f ? _appliedQualityWeight : ResolveGlobalQualityWeight01();
            return ambientSpawnRate * LerpClamped(0.45f, 1f, SmoothQuality01(qualityWeight));
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(0f, qualityWeight, math.isfinite(qualityWeight)));
        }

        private static float SmoothQuality01(float qualityWeight)
        {
            float q = math.saturate(qualityWeight);
            return q * q * (3f - 2f * q);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_slowTickRegistered)
                _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }
        }

        private void CacheRegistryServicesCold()
        {
            WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _sargassumDrag);
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
                case GlobalRegistryServiceSlot.SargassumDragRuntime:
                    _sargassumDrag = currentService as SargassumGlobalDragManager;
                    WorldRuntimeReferenceUtility.TryResolveSargassumGlobalDragManager(ref _sargassumDrag);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
            }
        }

        private void EmitAmbientParticle(Vector3 centerWS, float densityT, float window01)
        {
            bool emitBubble = Next01() < ambientBubbleChance * LerpClamped(0.9f, 1.15f, window01);
            float size = emitBubble ? bubbleSize * 0.42f : ambientSize;
            float lifetime = emitBubble ? bubbleLifetime * 1.65f : ambientLifetime;
            Color color = emitBubble
                ? _bubbleColor
                : Color.Lerp(_leafShadowColor, _leafHighlightColor, Next01() * 0.75f);

            EmitSingle(
                BuildAmbientPosition(centerWS),
                BuildAmbientVelocity(densityT),
                lifetime * LerpClamped(0.82f, 1.18f, Next01()),
                size * LerpClamped(0.78f, 1.22f, densityT),
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
            float lateralScale = ambientDriftSpeed * LerpClamped(0.7f, 1.15f, densityT);
            return new Vector3(
                NextSigned(lateralScale),
                ambientRiseSpeed * LerpClamped(0.8f, 1.2f, Next01()),
                NextSigned(lateralScale));
        }

        private void EmitGroup(Vector3 positionWS, Vector3 velocityWS, int count, float lifetime, float size, Color color)
        {
            if (count <= 0)
                return;

            QueueParticleEmit(
                positionWS,
                velocityWS + BuildJitterVector(),
                lifetime,
                size,
                color,
                NextSeed(),
                count);
        }

        private void EmitSingle(Vector3 positionWS, Vector3 velocityWS, float lifetime, float size, Color color)
        {
            QueueParticleEmit(positionWS, velocityWS, lifetime, size, color, NextSeed(), 1);
        }

        private void QueueParticleEmit(
            Vector3 positionWS,
            Vector3 velocityWS,
            float lifetime,
            float size,
            Color color,
            uint randomSeed,
            int count)
        {
            if (_queuedEmitCount >= MaxQueuedParticleEmits)
                return;

            int remainingParticleBudget = ResolveQueuedParticleBudget() - _queuedParticleCount;
            if (remainingParticleBudget <= 0)
                return;

            int emitCount = math.min(
                math.clamp(count, 1, MaxParticlesPerQueuedEmit),
                remainingParticleBudget);

            _queuedEmits[_queuedEmitCount++] = new PendingParticleEmit
            {
                PositionWS = positionWS,
                VelocityWS = velocityWS,
                Lifetime = lifetime,
                Size = size,
                Color = color,
                RandomSeed = randomSeed,
                Count = emitCount
            };
            _queuedParticleCount += emitCount;
        }

        private void FlushQueuedParticleEmits()
        {
            if (_queuedEmitCount <= 0 || _particleSystem == null)
            {
                _queuedEmitCount = 0;
                _queuedParticleCount = 0;
                return;
            }

            ParticleSystem.EmitParams emitParams = default;
            int safeCount = Mathf.Min(_queuedEmitCount, _queuedEmits.Length);
            for (int i = 0; i < safeCount; i++)
            {
                PendingParticleEmit queued = _queuedEmits[i];
                emitParams.position = queued.PositionWS;
                emitParams.velocity = queued.VelocityWS;
                emitParams.startLifetime = queued.Lifetime;
                emitParams.startSize = queued.Size;
                emitParams.startColor = queued.Color;
                emitParams.randomSeed = queued.RandomSeed;
                _particleSystem.Emit(emitParams, queued.Count);
                _queuedEmits[i] = default;
            }

            _queuedEmitCount = 0;
            _queuedParticleCount = 0;
        }

        private int ResolveQueuedParticleBudget()
        {
            if (_appliedQualityParticleCap > 0)
                return _appliedQualityParticleCap;

            return ResolveQualityParticleCap(ResolveGlobalQualityWeight01());
        }

        private Vector3 BuildJitterVector()
        {
            float jitterX = LerpClamped(-0.65f, 0.65f, Next01());
            float jitterY = LerpClamped(0.05f, 0.85f, Next01());
            float jitterZ = LerpClamped(-0.65f, 0.65f, Next01());
            return new Vector3(jitterX, jitterY, jitterZ);
        }

        private static float LerpClamped(float from, float to, float t)
        {
            return from + ((to - from) * math.saturate(t));
        }

        private static Vector3 ResolveSafeDirection(Vector3 direction, Vector3 fallback)
        {
            float lengthSq = direction.sqrMagnitude;
            return lengthSq > 0.0001f ? direction * math.rsqrt(lengthSq) : fallback;
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

        private struct PendingParticleEmit
        {
            public Color Color;
            public Vector3 PositionWS;
            public Vector3 VelocityWS;
            public float Lifetime;
            public float Size;
            public uint RandomSeed;
            public int Count;
        }
    }
}
