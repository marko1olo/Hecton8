using Hecton8.Core;
using Hecton8.Physics;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Environment
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Environment/Global Weather Director")]
    [DefaultExecutionOrder(-4550)]
    public sealed class GlobalWeatherDirector : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, IFrostTickable, IWeatherService
    {
        private const float ExponentialBlendCompletion = 0.99f;
        private const float ExponentialBlendRateScale = 4.6051702f;
        private const float TransitionCompletionThreshold = 0.999f;
        private const float CurrentSyncEpsilonSq = 0.000025f;
        private const float BiomeBlendChangeEpsilon = 0.0001f;
        private const float WeatherLutChangeEpsilon = 0.01f;
        private const float VectorNormalizeEpsilon = 0.0001f;
        private const float Lcg24BitToUnit = 1f / 16777216f;
        private const float AtmosphericBridgeFlowSurgeScale = 0.5f;
        private const float AtmosphericBridgeWaveReferenceEpsilon = 0.0001f;
        private const int NoirFogLutRowCount = 1;
        private const int MaxRuntimeNoirFogLutResolution = 64;
#if UNITY_EDITOR
        private const string CalmWeatherProfileAssetPath = "Assets/_Project/Data/Environment/Weather/WeatherProfile_DeepStillness.asset";
        private const string StormWeatherProfileAssetPath = "Assets/_Project/Data/Environment/Weather/WeatherProfile_CyclonicSurge.asset";
        private const string SurgeWeatherProfileAssetPath = "Assets/_Project/Data/Environment/Weather/WeatherProfile_BiolumeBloom.asset";
        private const string ShallowBiomeWeatherProfileAssetPath = "Assets/_Project/Data/Environment/Weather/WeatherProfile_ShallowLight.asset";
        private const string MidnightBiomeWeatherProfileAssetPath = "Assets/_Project/Data/Environment/Weather/WeatherProfile_MidnightStillness.asset";
        private const string HadalBiomeWeatherProfileAssetPath = "Assets/_Project/Data/Environment/Weather/WeatherProfile_HadalsSurge.asset";
#endif

        private enum WeatherPhase : byte
        {
            Calm = 0,
            Storm = 1,
            CurrentSurge = 2,
        }

        [System.Serializable]
        private struct PhaseProfile
        {
            [Tooltip("World-space wind direction used by this macro state.")]
            public Vector3 windDirection;
            [Tooltip("Wind speed in meters per second.")]
            public float windSpeed;
            [Tooltip("World-space current direction used by this macro state.")]
            public Vector3 currentDirection;
            [Tooltip("Current speed in meters per second.")]
            public float currentSpeed;
            [Tooltip("Thermocline / halocline response multiplier for downstream consumers.")]
            public float thermalIntensity;
            [Tooltip("Amplitude multiplier applied to the fallback Gerstner spectrum.")]
            public float waveAmplitudeScale;
            [Tooltip("Steepness multiplier applied to the fallback Gerstner spectrum.")]
            public float waveSteepnessScale;
            [Tooltip("Speed multiplier applied to the fallback Gerstner spectrum.")]
            public float waveSpeedScale;
            [Tooltip("Minimum hold time before this state can attempt a transition.")]
            public float minHoldSeconds;
            [Tooltip("Maximum hold time before this state can attempt a transition.")]
            public float maxHoldSeconds;
            [Tooltip("Probability of transitioning to the next macro state when the hold timer expires.")]
            public float transitionProbability;
        }

        [System.Serializable]
        private struct WaveBandAuthoring
        {
            [Tooltip("Normalized XZ travel direction for the fallback Gerstner band.")]
            public Vector2 directionXZ;
            [Tooltip("Band amplitude in meters.")]
            public float amplitude;
            [Tooltip("Band wavelength in meters.")]
            public float wavelength;
            [Tooltip("Band steepness used for horizontal correction.")]
            public float steepness;
            [Tooltip("Authoring-time phase offset in radians.")]
            public float phaseOffsetRadians;
            [Tooltip("Per-band phase-speed multiplier.")]
            public float speedMultiplier;
        }

        private static readonly int _GlobalCurrentVectorId = Shader.PropertyToID("_HectonGlobalCurrentVector");
        private static readonly int _GlobalWindVectorId = Shader.PropertyToID("_HectonGlobalWindVector");
        private static readonly int _GlobalWindId = Shader.PropertyToID("_GlobalWind");
        private static readonly int _WeatherIntensityId = Shader.PropertyToID("_HectonWeatherIntensity");
        private static readonly int _WeatherStateMaskId = Shader.PropertyToID("_HectonWeatherStateMask");
        private static readonly int _NoirFogLutId = Shader.PropertyToID("_NoirFogLUT");
        private static readonly int _NoirFogLutParamsId = Shader.PropertyToID("_HectonNoirFogLutParams");
        private static readonly int _NoirFogLutBlendId = Shader.PropertyToID("_HectonNoirFogLutBlend");
        private static readonly int _NoirFogStratificationId = Shader.PropertyToID("_HectonNoirFogStratification");
        private static readonly int _BiolumeSurgeThresholdId = Shader.PropertyToID("_HectonBiolumeSurgeThreshold");
        private static readonly int _AbyssalFogDensityId = Shader.PropertyToID("_AbyssalFogDensity");
        private static readonly int _MarineSnowOpacityId = Shader.PropertyToID("_MarineSnowOpacity");
        private static readonly int _AtmosphericBridgeParamsId = Shader.PropertyToID("_HectonAtmosphericBridgeParams");
        private static readonly int _AtmosphericBridgeParams2Id = Shader.PropertyToID("_HectonAtmosphericBridgeParams2");
        private static readonly int _GlobalFlowMagnitudeMultiplierId = Shader.PropertyToID("_HectonGlobalFlowMagnitudeMultiplier");
        private static readonly int _GodRayIntensityId = Shader.PropertyToID("_HectonGodRayIntensity");
        private static readonly int _GlobalWindDirectionId = Shader.PropertyToID("_HectonGlobalWindDirection");
        private static readonly int _ShadowCascadeFadeId = Shader.PropertyToID("_HectonShadowCascadeFade");
        private static readonly int _UnderwaterRainVolumeId = Shader.PropertyToID("_HectonUnderwaterRainVolume");
        private static readonly int _BiolumEmissionMultiplierId = Shader.PropertyToID("_HectonBiolumEmissionMultiplier");
        private static readonly int _RadiationStormId = Shader.PropertyToID("_HectonRadiationStorm");

        [Header("References")]
        [Tooltip("Optional explicit fluid-engine reference. If empty, the runtime singleton is used.")]
        [SerializeField] private HectonFluidEngine fluidEngine;

        [Header("State Machine")]
        [Tooltip("Random seed for deterministic weather rolls.")]
        [SerializeField] private uint randomSeed = 13081u;
        [Tooltip("Seconds used to blend from one macro state into the next.")]
        [SerializeField, Min(1f)] private float transitionDurationSeconds = 60f;
        [Tooltip("Optional authored calm-weather profile used to override wind and wave-height targets.")]
        [SerializeField] private WeatherProfile calmWeatherProfile;
        [Tooltip("Optional authored storm-weather profile used to override wind and wave-height targets.")]
        [SerializeField] private WeatherProfile stormWeatherProfile;
        [Tooltip("Optional authored surge-weather profile used to override wind and wave-height targets.")]
        [SerializeField] private WeatherProfile surgeWeatherProfile;
        [Tooltip("Authoring profile for the shallow-light biome LUT contribution.")]
        [SerializeField] private WeatherProfile shallowLightBiomeProfile;
        [Tooltip("Authoring profile for the midnight-stillness biome LUT contribution.")]
        [SerializeField] private WeatherProfile midnightStillnessBiomeProfile;
        [Tooltip("Authoring profile for the hadal-surge biome LUT contribution.")]
        [SerializeField] private WeatherProfile hadalsSurgeBiomeProfile;
        [Tooltip("Calm-state current, wind, and wave response profile.")]
        [SerializeField] private PhaseProfile calmProfile = new PhaseProfile
        {
            windDirection = new Vector3(1f, 0f, 0.1f),
            windSpeed = 3f,
            currentDirection = new Vector3(0.3f, -0.02f, 1f),
            currentSpeed = 0.35f,
            thermalIntensity = 0.1f,
            waveAmplitudeScale = 0.2f,
            waveSteepnessScale = 0.35f,
            waveSpeedScale = 0.8f,
            minHoldSeconds = 120f,
            maxHoldSeconds = 240f,
            transitionProbability = 0.4f,
        };
        [Tooltip("Storm-state current, wind, and wave response profile.")]
        [SerializeField] private PhaseProfile stormProfile = new PhaseProfile
        {
            windDirection = new Vector3(0.9f, 0f, 0.35f),
            windSpeed = 18f,
            currentDirection = new Vector3(0.4f, -0.08f, 1f),
            currentSpeed = 1.1f,
            thermalIntensity = 0.45f,
            waveAmplitudeScale = 0.85f,
            waveSteepnessScale = 0.9f,
            waveSpeedScale = 1.15f,
            minHoldSeconds = 75f,
            maxHoldSeconds = 150f,
            transitionProbability = 0.55f,
        };
        [Tooltip("Current-surge profile used after storms to intensify underwater flow.")]
        [SerializeField] private PhaseProfile surgeProfile = new PhaseProfile
        {
            windDirection = new Vector3(0.8f, 0f, 0.5f),
            windSpeed = 10f,
            currentDirection = new Vector3(0.55f, -0.15f, 1f),
            currentSpeed = 2f,
            thermalIntensity = 0.8f,
            waveAmplitudeScale = 1.15f,
            waveSteepnessScale = 1.05f,
            waveSpeedScale = 1.25f,
            minHoldSeconds = 60f,
            maxHoldSeconds = 135f,
            transitionProbability = 0.7f,
        };

        [Header("Fallback Wave Spectrum")]
        [Tooltip("Primary fallback Gerstner band for CPU-side buoyancy queries.")]
        [SerializeField] private WaveBandAuthoring wave0 = new WaveBandAuthoring
        {
            directionXZ = new Vector2(1f, 0f),
            amplitude = 0.55f,
            wavelength = 9f,
            steepness = 0.45f,
            phaseOffsetRadians = 0f,
            speedMultiplier = 1f,
        };
        [Tooltip("Secondary fallback Gerstner band for CPU-side buoyancy queries.")]
        [SerializeField] private WaveBandAuthoring wave1 = new WaveBandAuthoring
        {
            directionXZ = new Vector2(0.65f, 0.76f),
            amplitude = 0.28f,
            wavelength = 5.5f,
            steepness = 0.38f,
            phaseOffsetRadians = 1.2f,
            speedMultiplier = 1.05f,
        };
        [Tooltip("Tertiary fallback Gerstner band for CPU-side buoyancy queries.")]
        [SerializeField] private WaveBandAuthoring wave2 = new WaveBandAuthoring
        {
            directionXZ = new Vector2(-0.35f, 0.94f),
            amplitude = 0.16f,
            wavelength = 3.25f,
            steepness = 0.3f,
            phaseOffsetRadians = 2.1f,
            speedMultiplier = 1.15f,
        };

        [Header("Noir Fog LUT")]
        [Tooltip("Width of the runtime depth fog LUT. Hard-capped at 64 samples; weather drift is faked in shader ALU.")]
        [SerializeField, Range(8, MaxRuntimeNoirFogLutResolution)] private int noirFogLutResolution = 32;
        [Tooltip("Seconds used to exponentially settle the biome LUT blend factor.")]
        [SerializeField, Min(0.25f)] private float biomeBlendDurationSeconds = 8f;
        [Tooltip("World-space Y depth for the thermocline band passed into noir fog shading.")]
        [SerializeField] private float thermoclineY = -120f;
        [Tooltip("Half-width in meters for the visible thermocline fog band.")]
        [SerializeField, Min(0.1f)] private float thermoclineHalfSpanMeters = 10f;
        [Tooltip("Additional density multiplier applied inside the thermocline band.")]
        [SerializeField, Min(0f)] private float thermoclineAbyssalBoost = 0.8f;

        [Header("Atmospheric Bridge")]
        [Tooltip("Abyssal fog density published when macro weather is calm.")]
        [SerializeField, Range(0f, 1f)] private float clearAbyssalFogDensity = 0.18f;
        [Tooltip("Abyssal fog density published when storm/current weather is at full force.")]
        [SerializeField, Range(0f, 1f)] private float stormAbyssalFogDensity = 0.92f;
        [Tooltip("Marine snow opacity published when macro weather is calm.")]
        [SerializeField, Range(0f, 1f)] private float clearMarineSnowOpacity = 0.25f;
        [Tooltip("Marine snow opacity published when storm/current weather is at full force.")]
        [SerializeField, Range(0f, 1f)] private float stormMarineSnowOpacity = 1f;
        [Tooltip("Base scalar for shader-side god-ray shafts before lunar and wave modulation.")]
        [SerializeField, Range(0f, 2f)] private float baseGodRayIntensity = 0.55f;
        [Tooltip("Wave height that maps to full god-ray wave modulation.")]
        [SerializeField, Min(0.01f)] private float godRayWaveReferenceMeters = 2f;
        [Tooltip("Strength of deterministic triangle-wave cloud occlusion applied during storms.")]
        [SerializeField, Range(0f, 1f)] private float godRayCloudFlickerStrength = 0.35f;
        [Tooltip("Shadow cascade fade scalar at maximum fog density.")]
        [SerializeField, Range(0f, 1f)] private float shadowCascadeFadeAtMaxFog = 0.65f;
        [Tooltip("Underwater rain rumble volume published when weather intensity is one.")]
        [SerializeField, Range(0f, 1f)] private float underwaterRainVolumeAtStorm = 0.85f;

        [Header("Diagnostics")]
        [SerializeField] private WeatherPhase _debugActivePhase = WeatherPhase.Calm;
        [SerializeField] private WeatherPhase _debugTargetPhase = WeatherPhase.Calm;
        [SerializeField] private bool _debugTransitioning;
        [SerializeField] private float _debugPhaseHoldTimer;
        [SerializeField] private float _weatherIntensity = 1f;
        [SerializeField] private uint _debugStateMask = (uint)WeatherState.Calm;
        [SerializeField] private Vector3 _debugCurrentVector;
        [SerializeField] private Vector3 _debugWindVector;

        private bool _registeredToTickManager;
        private bool _registeredToFrostTickManager;
        private bool _initialized;
        private bool _transitioning;
        private float _phaseHoldTimer;
        private float _transitionTimer;
        private WeatherPhase _activePhase = WeatherPhase.Calm;
        private WeatherPhase _sourcePhase = WeatherPhase.Calm;
        private WeatherPhase _targetPhase = WeatherPhase.Calm;
        private float _biolumeSurgeTimer;
        private WeatherRuntimeSnapshot _runtimeSnapshot;
        private float3 _lastAppliedCurrentVector;
        private uint _weatherRandomState;
        private Texture2D _noirFogLutTexture;
        private Color[] _noirFogLutPixels;
        private WeatherProfile _activeBiomeLutSourceProfile;
        private WeatherProfile _activeBiomeLutTargetProfile;
        private WeatherProfile _activeWeatherLutSourceProfile;
        private WeatherProfile _activeWeatherLutTargetProfile;
        private float _activeWeatherLutInfluence;
        private float _biomeLutBlend;
        private float _publishedBiolumeSurgeThreshold = 8f;
        private float3 _lastWeatherEventCurrentVector;
        private float3 _lastWeatherEventWindVector;
        private uint _lastWeatherEventStateMask;
        private float _lastWeatherEventIntensity;
        private bool _hasPublishedWeatherEvent;

        /// <summary>
        /// True once the director has seeded its state and registered through <see cref="GlobalRegistry"/>.
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Active weather-state bitmask for the current frame.
        /// </summary>
        public WeatherState CurrentWeatherState => _runtimeSnapshot.StateMask;

        /// <summary>
        /// Global current vector resolved for the current frame.
        /// </summary>
        public Vector3 GlobalCurrentVector => new Vector3(
            _runtimeSnapshot.GlobalCurrentVector.x,
            _runtimeSnapshot.GlobalCurrentVector.y,
            _runtimeSnapshot.GlobalCurrentVector.z);

        /// <summary>
        /// Global wind vector resolved for the current frame.
        /// </summary>
        public Vector3 GlobalWindVector => new Vector3(
            _runtimeSnapshot.GlobalWindVector.x,
            _runtimeSnapshot.GlobalWindVector.y,
            _runtimeSnapshot.GlobalWindVector.z);

        /// <summary>
        /// Normalized storm/current intensity after active weather-state blending.
        /// </summary>
        public float WeatherIntensity => _runtimeSnapshot.WeatherIntensity;

        private void Awake()
        {
            SeedRandom();
            ResolveDependencies();
            InitializeRuntimeStateIfNeeded();
            UpdateBiomeLutState(transitionDurationSeconds, true);
            PublishSnapshot();
            PublishAtmosphericBridgeShaderState();
        }

        private void OnEnable()
        {
            TryRegisterTickManager();
            ResolveDependencies();
            InitializeRuntimeStateIfNeeded();
            UpdateBiomeLutState(transitionDurationSeconds, true);
            GlobalRegistry.RegisterWeatherService(this);
            PublishSnapshot();
            PublishAtmosphericBridgeShaderState();
        }

        private void Start()
        {
            TryRegisterTickManager();
        }

        private void OnDisable()
        {
            TryUnregisterTickManager();
            if (ReferenceEquals(GlobalRegistry.Weather, this))
                GlobalRegistry.UnregisterWeatherService(this);
            _biolumeSurgeTimer = 0f;
            Shader.SetGlobalVector(_GlobalCurrentVectorId, Vector4.zero);
            Shader.SetGlobalVector(_GlobalWindVectorId, Vector4.zero);
            Shader.SetGlobalVector(_GlobalWindId, Vector4.zero);
            Shader.SetGlobalFloat(_WeatherIntensityId, 0f);
            Shader.SetGlobalInt(_WeatherStateMaskId, 0);
            Shader.SetGlobalTexture(_NoirFogLutId, Texture2D.blackTexture);
            Shader.SetGlobalVector(_NoirFogLutParamsId, Vector4.zero);
            Shader.SetGlobalFloat(_NoirFogLutBlendId, 0f);
            Shader.SetGlobalVector(_NoirFogStratificationId, Vector4.zero);
            Shader.SetGlobalFloat(_BiolumeSurgeThresholdId, 0f);
            ClearAtmosphericBridgeShaderState();
            _hasPublishedWeatherEvent = false;
        }

        private void OnDestroy()
        {
            TryUnregisterTickManager();
            if (ReferenceEquals(GlobalRegistry.Weather, this))
                GlobalRegistry.UnregisterWeatherService(this);
            ReleaseNoirFogLutResources();
        }

        /// <summary>
        /// Advances transition state and publishes resolved wind/current vectors every frame.
        /// </summary>
        /// <param name="deltaTime">Shared tick delta.</param>
        public void Tick(float deltaTime)
        {
            TryRegisterTickManager();
            ResolveDependencies();
            InitializeRuntimeStateIfNeeded();

            if (!_initialized)
                return;

            _runtimeSnapshot.CurrentMeta.TimeAccumulator += math.max(0f, deltaTime);
            _biolumeSurgeTimer = math.max(0f, _biolumeSurgeTimer - math.max(0f, deltaTime));
            AdvanceTransition(deltaTime);
            UpdateBiomeLutState(deltaTime, false);
            PublishSnapshot();
        }

        /// <summary>
        /// Evaluates probabilistic macro-state transitions on the shared slow-tick cadence.
        /// </summary>
        public void SlowTick()
        {
            InitializeRuntimeStateIfNeeded();
            if (!_initialized)
                return;

            if (_transitioning)
            {
                PublishSnapshot();
                return;
            }

            float slowTickDelta = ResolveSlowTickInterval();
            _phaseHoldTimer -= slowTickDelta;
            _debugPhaseHoldTimer = _phaseHoldTimer;
            if (_phaseHoldTimer > 0f)
            {
                PublishSnapshot();
                return;
            }

            PhaseProfile activeProfile = GetProfile(_activePhase);
            if (NextRandom01() > math.saturate(activeProfile.transitionProbability))
            {
                _phaseHoldTimer = ResolveHoldDuration(activeProfile) * 0.5f;
                _debugPhaseHoldTimer = _phaseHoldTimer;
                PublishSnapshot();
                return;
            }

            BeginTransition(ResolveNextPhase(_activePhase));
            PublishSnapshot();
        }

        /// <summary>
        /// Publishes low-cadence visual bridge parameters for abyssal fog, snow, god-rays, and rain rumble.
        /// </summary>
        public void FrostTick()
        {
            InitializeRuntimeStateIfNeeded();
            if (!_initialized)
                return;

            PublishAtmosphericBridgeShaderState();
        }

        /// <summary>
        /// Returns the current zero-allocation runtime snapshot for downstream systems.
        /// </summary>
        /// <returns>Weather state, vectors, current metadata, and fallback-wave data.</returns>
        public WeatherRuntimeSnapshot GetRuntimeSnapshot()
        {
            return _runtimeSnapshot;
        }

        /// <summary>
        /// Forces the bioluminescent surge bit high for the requested duration.
        /// </summary>
        /// <param name="durationSeconds">Surge hold duration in seconds.</param>
        public void RegisterBiolumeSurge(float durationSeconds)
        {
            _biolumeSurgeTimer = math.max(_biolumeSurgeTimer, math.max(0.1f, durationSeconds));
        }

        private void TryRegisterTickManager()
        {
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredToTickManager)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _registeredToTickManager = GlobalRegistry.Updatables.Contains(this) ||
                                           GlobalRegistry.SlowTickables.Contains(this);
            }

            if (!_registeredToFrostTickManager)
            {
                GlobalRegistry.RegisterFrostTickable(this, PriorityLayer.Environment);
                _registeredToFrostTickManager = GlobalRegistry.FrostTickables.Contains(this);
            }
        }

        private void TryUnregisterTickManager()
        {
            if (_registeredToTickManager)
            {
                if (GlobalRegistry.Updatables.Contains(this))
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

                if (GlobalRegistry.SlowTickables.Contains(this))
                    GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (_registeredToFrostTickManager && GlobalRegistry.FrostTickables.Contains(this))
                GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
            _registeredToFrostTickManager = false;
        }

        private void ResolveDependencies()
        {
            if (fluidEngine == null)
                fluidEngine = GlobalRegistry.Fluid;
        }

        private void InitializeRuntimeStateIfNeeded()
        {
            if (_initialized)
                return;

            _activePhase = WeatherPhase.Calm;
            _sourcePhase = WeatherPhase.Calm;
            _targetPhase = WeatherPhase.Calm;
            _transitioning = false;
            _transitionTimer = 0f;
            _weatherIntensity = 1f;
            _phaseHoldTimer = ResolveHoldDuration(GetProfile(_activePhase));
            _runtimeSnapshot = default;
            _biomeLutBlend = 0f;
            _activeBiomeLutSourceProfile = null;
            _activeBiomeLutTargetProfile = null;
            _activeWeatherLutSourceProfile = null;
            _activeWeatherLutTargetProfile = null;
            _activeWeatherLutInfluence = 0f;
            _initialized = true;
        }

        private void SeedRandom()
        {
            uint seed = randomSeed == 0u ? 1u : randomSeed;
            _weatherRandomState = seed;
        }

        private void AdvanceTransition(float deltaTime)
        {
            if (!_transitioning)
            {
                _weatherIntensity = 1f;
                return;
            }

            float clampedDeltaTime = math.max(0f, deltaTime);
            _transitionTimer += clampedDeltaTime;
            _weatherIntensity = math.lerp(
                _weatherIntensity,
                1f,
                ResolveExponentialBlendFactor(clampedDeltaTime, transitionDurationSeconds));
            if (_weatherIntensity < TransitionCompletionThreshold)
                return;

            _activePhase = _targetPhase;
            _sourcePhase = _activePhase;
            _targetPhase = _activePhase;
            _transitioning = false;
            _transitionTimer = 0f;
            _weatherIntensity = 1f;
            _phaseHoldTimer = ResolveHoldDuration(GetProfile(_activePhase));
            _debugPhaseHoldTimer = _phaseHoldTimer;
        }

        private void BeginTransition(WeatherPhase nextPhase)
        {
            _sourcePhase = _activePhase;
            _targetPhase = nextPhase;
            _transitioning = true;
            _transitionTimer = 0f;
            _weatherIntensity = 0f;
            _debugTargetPhase = _targetPhase;
        }

        private void PublishSnapshot()
        {
            PhaseProfile fromProfile = GetProfile(_transitioning ? _sourcePhase : _activePhase);
            PhaseProfile toProfile = GetProfile(_targetPhase);
            WeatherProfile fromWeatherProfile = GetWeatherProfile(_transitioning ? _sourcePhase : _activePhase);
            WeatherProfile toWeatherProfile = GetWeatherProfile(_targetPhase);
            float blend = _transitioning ? _weatherIntensity : 1f;
            float weatherIntensity01 = ResolveWeatherIntensity01(blend);

            float3 currentVector = math.lerp(
                ResolveDirectionalVector(fromProfile.currentDirection, fromProfile.currentSpeed),
                ResolveDirectionalVector(toProfile.currentDirection, toProfile.currentSpeed),
                blend);
            float3 windVector = math.lerp(
                ResolveDirectionalVector(fromProfile.windDirection, fromProfile.windSpeed),
                ResolveDirectionalVector(toProfile.windDirection, toProfile.windSpeed),
                blend);
            float thermalIntensity = math.lerp(fromProfile.thermalIntensity, toProfile.thermalIntensity, blend);
            float amplitudeScale = math.lerp(fromProfile.waveAmplitudeScale, toProfile.waveAmplitudeScale, blend);
            float steepnessScale = math.lerp(fromProfile.waveSteepnessScale, toProfile.waveSteepnessScale, blend);
            float speedScale = math.lerp(fromProfile.waveSpeedScale, toProfile.waveSpeedScale, blend);
            if (fromWeatherProfile != null || toWeatherProfile != null)
            {
                windVector = math.lerp(
                    ResolveWeatherProfileWindVector(fromWeatherProfile, windVector),
                    ResolveWeatherProfileWindVector(toWeatherProfile, windVector),
                    blend);

                float authoredWaveHeight = math.lerp(
                    ResolveWeatherProfileWaveHeight(fromWeatherProfile, amplitudeScale),
                    ResolveWeatherProfileWaveHeight(toWeatherProfile, amplitudeScale),
                    blend);
                float totalBaseWaveHeight = ResolveBaseWaveHeight();
                if (authoredWaveHeight > VectorNormalizeEpsilon && totalBaseWaveHeight > VectorNormalizeEpsilon)
                    amplitudeScale = authoredWaveHeight / totalBaseWaveHeight;

                float authoredTurbulenceMultiplier = math.lerp(
                    ResolveWeatherProfileTurbulenceMultiplier(fromWeatherProfile),
                    ResolveWeatherProfileTurbulenceMultiplier(toWeatherProfile),
                    blend);
                thermalIntensity *= math.max(0f, authoredTurbulenceMultiplier);
            }

            currentVector *= ResolveWeatherFlowMagnitudeMultiplier(weatherIntensity01);

            CurrentMeta currentMeta = _runtimeSnapshot.CurrentMeta;
            currentMeta.GlobalBaseVector = NormalizeSafe(currentVector, new float3(0f, 0f, 1f));
            currentMeta.GlobalScale = ApproximateMagnitude(currentVector);
            currentMeta.ThermalIntensity = math.max(0f, thermalIntensity);

            _runtimeSnapshot.StateMask = ResolvePublishedMask();
            _runtimeSnapshot.WeatherIntensity = weatherIntensity01;
            _runtimeSnapshot.GlobalCurrentVector = currentVector;
            _runtimeSnapshot.GlobalWindVector = windVector;
            _runtimeSnapshot.CurrentMeta = currentMeta;
            _runtimeSnapshot.Wave0 = ResolveWaveComponent(wave0, amplitudeScale, steepnessScale, speedScale);
            _runtimeSnapshot.Wave1 = ResolveWaveComponent(wave1, amplitudeScale, steepnessScale, speedScale);
            _runtimeSnapshot.Wave2 = ResolveWaveComponent(wave2, amplitudeScale, steepnessScale, speedScale);

            Vector3 currentVectorManaged = new Vector3(currentVector.x, currentVector.y, currentVector.z);
            Vector3 windVectorManaged = new Vector3(windVector.x, windVector.y, windVector.z);
            Shader.SetGlobalVector(_GlobalCurrentVectorId, new Vector4(currentVectorManaged.x, currentVectorManaged.y, currentVectorManaged.z, 0f));
            Shader.SetGlobalVector(_GlobalWindVectorId, new Vector4(windVectorManaged.x, windVectorManaged.y, windVectorManaged.z, 0f));
            Shader.SetGlobalVector(_GlobalWindId, new Vector4(windVectorManaged.x, windVectorManaged.y, windVectorManaged.z, ApproximateMagnitude(windVector)));
            Shader.SetGlobalFloat(_WeatherIntensityId, weatherIntensity01);
            Shader.SetGlobalInt(_WeatherStateMaskId, (int)_runtimeSnapshot.StateMask);
            PublishNoirFogShaderState();

            if (fluidEngine != null && math.lengthsq(currentVector - _lastAppliedCurrentVector) > CurrentSyncEpsilonSq)
            {
                fluidEngine.CurrentVector = currentVectorManaged;
                fluidEngine.CurrentStrength = 1f;
                _lastAppliedCurrentVector = currentVector;
            }

            _debugActivePhase = _activePhase;
            _debugTargetPhase = _targetPhase;
            _debugTransitioning = _transitioning;
            _debugStateMask = (uint)_runtimeSnapshot.StateMask;
            _debugCurrentVector = currentVectorManaged;
            _debugWindVector = windVectorManaged;
            _debugPhaseHoldTimer = _phaseHoldTimer;
            PublishWeatherEventIfChanged();
        }

        private void PublishWeatherEventIfChanged()
        {
            uint stateMask = (uint)_runtimeSnapshot.StateMask;
            if (_hasPublishedWeatherEvent &&
                stateMask == _lastWeatherEventStateMask &&
                math.abs(_runtimeSnapshot.WeatherIntensity - _lastWeatherEventIntensity) <= WeatherLutChangeEpsilon &&
                math.lengthsq(_runtimeSnapshot.GlobalCurrentVector - _lastWeatherEventCurrentVector) <= CurrentSyncEpsilonSq &&
                math.lengthsq(_runtimeSnapshot.GlobalWindVector - _lastWeatherEventWindVector) <= CurrentSyncEpsilonSq)
            {
                return;
            }

            _hasPublishedWeatherEvent = true;
            _lastWeatherEventStateMask = stateMask;
            _lastWeatherEventIntensity = _runtimeSnapshot.WeatherIntensity;
            _lastWeatherEventCurrentVector = _runtimeSnapshot.GlobalCurrentVector;
            _lastWeatherEventWindVector = _runtimeSnapshot.GlobalWindVector;
            WeatherEvents.RaiseSnapshotUpdated(in _runtimeSnapshot);
        }

        private void UpdateBiomeLutState(float deltaTime, bool forceImmediate)
        {
            EnsureNoirFogLutResources();
            if (_noirFogLutTexture == null || _noirFogLutPixels == null)
                return;

            ResolveBiomeLutProfiles(out WeatherProfile sourceProfile, out WeatherProfile targetProfile, out float targetBlend);
            ResolveWeatherLutProfiles(out WeatherProfile weatherSourceProfile, out WeatherProfile weatherTargetProfile, out float weatherInfluence);
            bool sourceChanged = !ReferenceEquals(sourceProfile, _activeBiomeLutSourceProfile);
            bool targetChanged = !ReferenceEquals(targetProfile, _activeBiomeLutTargetProfile);
            bool weatherSourceChanged = !ReferenceEquals(weatherSourceProfile, _activeWeatherLutSourceProfile);
            bool weatherTargetChanged = !ReferenceEquals(weatherTargetProfile, _activeWeatherLutTargetProfile);
            if (sourceChanged || targetChanged || weatherSourceChanged || weatherTargetChanged)
            {
                _activeBiomeLutSourceProfile = sourceProfile;
                _activeBiomeLutTargetProfile = targetProfile;
                _activeWeatherLutSourceProfile = weatherSourceProfile;
                _activeWeatherLutTargetProfile = weatherTargetProfile;
                _activeWeatherLutInfluence = weatherInfluence;
                RebuildNoirFogLutTexture(sourceProfile, targetProfile, weatherSourceProfile, weatherTargetProfile, weatherInfluence);
            }
            else
            {
                _activeWeatherLutInfluence = weatherInfluence;
            }

            if (forceImmediate)
            {
                _biomeLutBlend = targetBlend;
            }
            else
            {
                float blendFactor = ResolveExponentialBlendFactor(deltaTime, biomeBlendDurationSeconds);
                _biomeLutBlend = math.lerp(_biomeLutBlend, targetBlend, blendFactor);
                if (math.abs(_biomeLutBlend - targetBlend) <= BiomeBlendChangeEpsilon)
                    _biomeLutBlend = targetBlend;
            }

            _publishedBiolumeSurgeThreshold = math.lerp(
                ResolveBiolumeThreshold(sourceProfile),
                ResolveBiolumeThreshold(targetProfile),
                _biomeLutBlend);
        }

        private void EnsureNoirFogLutResources()
        {
            int resolution = math.clamp(noirFogLutResolution, 8, MaxRuntimeNoirFogLutResolution);
            if (_noirFogLutTexture != null &&
                _noirFogLutTexture.width == resolution &&
                _noirFogLutTexture.height == NoirFogLutRowCount &&
                _noirFogLutPixels != null &&
                _noirFogLutPixels.Length == resolution * NoirFogLutRowCount)
            {
                return;
            }

            ReleaseNoirFogLutResources();
            _noirFogLutTexture = new Texture2D(resolution, NoirFogLutRowCount, TextureFormat.RGBA32, false, true)
            {
                name = "Runtime_NoirFogLUT",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            // COLD ALLOC: Color[resolution * NoirFogLutRowCount] — runtime noir fog LUT staging buffer — owner: GlobalWeatherDirector
            _noirFogLutPixels = new Color[resolution * NoirFogLutRowCount];
        }

        private void ReleaseNoirFogLutResources()
        {
            _noirFogLutPixels = null;
            if (_noirFogLutTexture == null)
                return;

            if (Application.isPlaying)
                Destroy(_noirFogLutTexture);
            else
                DestroyImmediate(_noirFogLutTexture);

            _noirFogLutTexture = null;
        }

        private void ResolveBiomeLutProfiles(out WeatherProfile sourceProfile, out WeatherProfile targetProfile, out float blend)
        {
            WeatherProfile shallow = shallowLightBiomeProfile != null ? shallowLightBiomeProfile : calmWeatherProfile;
            WeatherProfile midnight = midnightStillnessBiomeProfile != null ? midnightStillnessBiomeProfile : stormWeatherProfile;
            WeatherProfile hadal = hadalsSurgeBiomeProfile != null ? hadalsSurgeBiomeProfile : surgeWeatherProfile;

            float shallowCenter = ResolveProfileDepthCenter(shallow, 90f);
            float midnightCenter = math.max(shallowCenter + 1f, ResolveProfileDepthCenter(midnight, 900f));
            float hadalCenter = math.max(midnightCenter + 1f, ResolveProfileDepthCenter(hadal, 3200f));
            float currentDepthMeters = ResolveCurrentBiomeDepthMeters();

            if (currentDepthMeters <= midnightCenter)
            {
                sourceProfile = shallow;
                targetProfile = midnight;
                blend = math.saturate(math.unlerp(shallowCenter, midnightCenter, currentDepthMeters));
                return;
            }

            sourceProfile = midnight;
            targetProfile = hadal;
            blend = math.saturate(math.unlerp(midnightCenter, hadalCenter, currentDepthMeters));
        }

        private float ResolveCurrentBiomeDepthMeters()
        {
            BiomeMatrixDirector biomeMatrix = BiomeMatrixDirector.ActiveRuntimeInstance;
            return biomeMatrix != null ? math.max(0f, biomeMatrix.CurrentDepthMeters) : 0f;
        }

        private void ResolveWeatherLutProfiles(out WeatherProfile sourceProfile, out WeatherProfile targetProfile, out float influence)
        {
            PhaseProfile fromPhaseProfile = GetProfile(_transitioning ? _sourcePhase : _activePhase);
            PhaseProfile toPhaseProfile = GetProfile(_targetPhase);
            sourceProfile = GetWeatherProfile(_transitioning ? _sourcePhase : _activePhase);
            targetProfile = GetWeatherProfile(_targetPhase);

            float sourceStrength = ResolveWeatherLutStrength(
                sourceProfile,
                ResolveDirectionalVector(fromPhaseProfile.windDirection, fromPhaseProfile.windSpeed),
                ResolveWeatherProfileWaveHeight(sourceProfile, fromPhaseProfile.waveAmplitudeScale));
            float targetStrength = ResolveWeatherLutStrength(
                targetProfile,
                ResolveDirectionalVector(toPhaseProfile.windDirection, toPhaseProfile.windSpeed),
                ResolveWeatherProfileWaveHeight(targetProfile, toPhaseProfile.waveAmplitudeScale));
            float weatherBlend = _transitioning ? _weatherIntensity : 1f;
            influence = math.lerp(sourceStrength, targetStrength, weatherBlend);
        }

        private void RebuildNoirFogLutTexture(
            WeatherProfile sourceProfile,
            WeatherProfile targetProfile,
            WeatherProfile weatherSourceProfile,
            WeatherProfile weatherTargetProfile,
            float weatherInfluence)
        {
            int width = _noirFogLutTexture.width;
            FillNoirFogLutRow(sourceProfile, targetProfile, width, weatherSourceProfile, weatherTargetProfile, weatherInfluence);
            _noirFogLutTexture.SetPixels(_noirFogLutPixels);
            _noirFogLutTexture.Apply(false, false);
        }

        private void FillNoirFogLutRow(
            WeatherProfile sourceProfile,
            WeatherProfile targetProfile,
            int width,
            WeatherProfile weatherSourceProfile,
            WeatherProfile weatherTargetProfile,
            float weatherInfluence)
        {
            float clampedWeatherInfluence = math.saturate(weatherInfluence);
            float biomeBlend = math.saturate(_biomeLutBlend);

            for (int x = 0; x < width; x++)
            {
                float t = width > 1 ? (float)x / (width - 1) : 0f;
                Color sourceFog = ResolveFogLutColor(sourceProfile, weatherSourceProfile, t, clampedWeatherInfluence);
                Color targetFog = ResolveFogLutColor(targetProfile, weatherTargetProfile, t, clampedWeatherInfluence);
                _noirFogLutPixels[x] = ClampOpaqueColor(LerpColorClamped(sourceFog, targetFog, biomeBlend));
            }
        }

        private static Color ResolveFogLutColor(WeatherProfile profile, WeatherProfile weatherProfile, float t, float weatherInfluence)
        {
            Color fogNear = profile != null ? profile.FogColorNear : new Color(0.03f, 0.09f, 0.14f, 1f);
            Color fogFar = profile != null ? profile.FogColorFar : new Color(0.01f, 0.04f, 0.08f, 1f);
            Texture2D authoredFogLut = profile != null ? profile.FogColorLut : null;
            Color weatherFogNear = weatherProfile != null ? weatherProfile.FogColorNear : fogNear;
            Color weatherFogFar = weatherProfile != null ? weatherProfile.FogColorFar : fogFar;
            Color fogSample = LerpColorClamped(fogNear, fogFar, t);
            if (authoredFogLut != null && authoredFogLut.isReadable)
            {
                Color authoredSample = authoredFogLut.GetPixelBilinear(t, 0.5f);
                authoredSample.a = 1f;
                fogSample = LerpColorClamped(fogSample, authoredSample, 0.5f);
            }

            return LerpColorClamped(fogSample, LerpColorClamped(weatherFogNear, weatherFogFar, t), weatherInfluence);
        }

        private void PublishNoirFogShaderState()
        {
            Shader.SetGlobalTexture(_NoirFogLutId, _noirFogLutTexture != null ? _noirFogLutTexture : Texture2D.blackTexture);
            Shader.SetGlobalVector(
                _NoirFogLutParamsId,
                new Vector4(
                    _noirFogLutTexture != null ? _noirFogLutTexture.width : 0f,
                    NoirFogLutRowCount,
                    _noirFogLutTexture != null ? 1f / math.max(1f, _noirFogLutTexture.width - 1f) : 0f,
                    _noirFogLutTexture != null ? 1f : 0f));
            Shader.SetGlobalFloat(_NoirFogLutBlendId, _biomeLutBlend);
            Shader.SetGlobalVector(
                _NoirFogStratificationId,
                new Vector4(
                    thermoclineY,
                    math.max(0.1f, thermoclineHalfSpanMeters),
                    math.max(0f, thermoclineAbyssalBoost),
                    math.max(0.0001f, RenderSettings.fogDensity)));
            Shader.SetGlobalFloat(_BiolumeSurgeThresholdId, _publishedBiolumeSurgeThreshold);
        }

        private void PublishAtmosphericBridgeShaderState()
        {
            CelestialRuntimeSnapshot celestialSnapshot = GlobalRegistry.CelestialRuntimeSnapshot;
            float weatherIntensity01 = math.saturate(_runtimeSnapshot.WeatherIntensity);
            float abyssalFogDensity = math.lerp(
                math.saturate(clearAbyssalFogDensity),
                math.saturate(stormAbyssalFogDensity),
                weatherIntensity01);
            float marineSnowOpacity = math.lerp(
                math.saturate(clearMarineSnowOpacity),
                math.saturate(stormMarineSnowOpacity),
                weatherIntensity01);
            float waveHeightMeters = ResolvePublishedWaveHeightMeters(in _runtimeSnapshot);
            float waveHeight01 = math.saturate(waveHeightMeters * math.rcp(math.max(AtmosphericBridgeWaveReferenceEpsilon, godRayWaveReferenceMeters)));
            float moonPhase01 = math.saturate(math.max(celestialSnapshot.Moon0Phase01, celestialSnapshot.Moon1Phase01));
            float cloudTriangle = math.lerp(0.55f, 1f, ResolveTriangleWave01(ResolveAtmosphericBridgeTimePhase(weatherIntensity01)));
            float cloudOcclusion = math.lerp(1f, cloudTriangle, math.saturate(godRayCloudFlickerStrength) * weatherIntensity01);
            float godRayIntensity = math.max(0f, baseGodRayIntensity) *
                                    math.lerp(0.35f, 1f, moonPhase01) *
                                    math.lerp(0.75f, 1.15f, waveHeight01) *
                                    cloudOcclusion;
            float globalFlowMultiplier = ResolveWeatherFlowMagnitudeMultiplier(weatherIntensity01);
            float shadowCascadeFade = abyssalFogDensity * math.saturate(shadowCascadeFadeAtMaxFog);
            float underwaterRainVolume = weatherIntensity01 * math.saturate(underwaterRainVolumeAtStorm);
            float windMagnitude = ApproximateMagnitude(_runtimeSnapshot.GlobalWindVector);
            float3 windDirection = NormalizeSafe(_runtimeSnapshot.GlobalWindVector, new float3(0f, 0f, 1f));
            float radiationStorm01 = math.saturate(celestialSnapshot.RadiationStorm01);
            float biolumEmissionMultiplier = ResolveBiolumEmissionMultiplier(in celestialSnapshot);

            Shader.SetGlobalFloat(_AbyssalFogDensityId, abyssalFogDensity);
            Shader.SetGlobalFloat(_MarineSnowOpacityId, marineSnowOpacity);
            Shader.SetGlobalFloat(_GlobalFlowMagnitudeMultiplierId, globalFlowMultiplier);
            Shader.SetGlobalFloat(_GodRayIntensityId, godRayIntensity);
            Shader.SetGlobalFloat(_ShadowCascadeFadeId, shadowCascadeFade);
            Shader.SetGlobalFloat(_UnderwaterRainVolumeId, underwaterRainVolume);
            Shader.SetGlobalFloat(_BiolumEmissionMultiplierId, biolumEmissionMultiplier);
            Shader.SetGlobalFloat(_RadiationStormId, radiationStorm01);
            Shader.SetGlobalVector(
                _AtmosphericBridgeParamsId,
                new Vector4(weatherIntensity01, abyssalFogDensity, marineSnowOpacity, globalFlowMultiplier));
            Shader.SetGlobalVector(
                _AtmosphericBridgeParams2Id,
                new Vector4(godRayIntensity, waveHeightMeters, moonPhase01, radiationStorm01));
            Shader.SetGlobalVector(
                _GlobalWindDirectionId,
                new Vector4(windDirection.x, windDirection.y, windDirection.z, windMagnitude));
        }

        private static void ClearAtmosphericBridgeShaderState()
        {
            Shader.SetGlobalFloat(_AbyssalFogDensityId, 0f);
            Shader.SetGlobalFloat(_MarineSnowOpacityId, 0f);
            Shader.SetGlobalFloat(_GlobalFlowMagnitudeMultiplierId, 1f);
            Shader.SetGlobalFloat(_GodRayIntensityId, 0f);
            Shader.SetGlobalFloat(_ShadowCascadeFadeId, 0f);
            Shader.SetGlobalFloat(_UnderwaterRainVolumeId, 0f);
            Shader.SetGlobalFloat(_BiolumEmissionMultiplierId, 1f);
            Shader.SetGlobalFloat(_RadiationStormId, 0f);
            Shader.SetGlobalVector(_AtmosphericBridgeParamsId, new Vector4(0f, 0f, 0f, 1f));
            Shader.SetGlobalVector(_AtmosphericBridgeParams2Id, Vector4.zero);
            Shader.SetGlobalVector(_GlobalWindDirectionId, Vector4.zero);
        }

        private float ResolveWeatherIntensity01(float transitionBlend)
        {
            if (!_transitioning)
                return IsIntenseWeatherPhase(_activePhase) ? 1f : 0f;

            float sourceIntensity = IsIntenseWeatherPhase(_sourcePhase) ? 1f : 0f;
            float targetIntensity = IsIntenseWeatherPhase(_targetPhase) ? 1f : 0f;
            return math.lerp(sourceIntensity, targetIntensity, math.saturate(transitionBlend));
        }

        private static bool IsIntenseWeatherPhase(WeatherPhase phase)
        {
            return phase == WeatherPhase.Storm || phase == WeatherPhase.CurrentSurge;
        }

        private static float ResolveWeatherFlowMagnitudeMultiplier(float weatherIntensity01)
        {
            return 1f + math.saturate(weatherIntensity01) * AtmosphericBridgeFlowSurgeScale;
        }

        private static float ResolvePublishedWaveHeightMeters(in WeatherRuntimeSnapshot snapshot)
        {
            return math.max(0f, snapshot.Wave0.Amplitude) +
                   math.max(0f, snapshot.Wave1.Amplitude) +
                   math.max(0f, snapshot.Wave2.Amplitude);
        }

        private static float ResolveBiolumEmissionMultiplier(in CelestialRuntimeSnapshot celestialSnapshot)
        {
            float authoredMultiplier = math.max(0f, celestialSnapshot.GlobalBiolumMultiplier);
            return authoredMultiplier > 0f ? authoredMultiplier : 1f;
        }

        private static float ResolveAtmosphericBridgeTimePhase(float weatherIntensity01)
        {
            double universeTime = GlobalRegistry.AbsoluteUniverseTime;
            if (!math.isfinite(universeTime))
                return weatherIntensity01 * 0.37f;

            double wrappedTime = universeTime - math.floor(universeTime * 0.000244140625d) * 4096d;
            return (float)wrappedTime * 0.07f + weatherIntensity01 * 0.37f;
        }

        private static float ResolveTriangleWave01(float phase)
        {
            float wrapped = phase - math.floor(phase);
            return 1f - math.abs(wrapped * 2f - 1f);
        }

        private static float ResolveProfileDepthCenter(WeatherProfile profile, float fallbackCenter)
        {
            if (profile == null)
                return fallbackCenter;

            float minDepth = math.max(0f, profile.MinDepthMeters);
            float maxDepth = math.max(minDepth + 1f, profile.MaxDepthMeters);
            return (minDepth + maxDepth) * 0.5f;
        }

        private static float ResolveBiolumeThreshold(WeatherProfile profile)
        {
            return profile != null ? math.max(0.1f, profile.BiolumeSurgeThreshold) : 8f;
        }

        private static float ResolveWeatherLutStrength(WeatherProfile profile, float3 fallbackWindVector, float resolvedWaveHeight)
        {
            float windMagnitude = ApproximateMagnitude(ResolveWeatherProfileWindVector(profile, fallbackWindVector));
            return math.saturate((windMagnitude * 0.04f) + (math.max(0f, resolvedWaveHeight) * 0.08f));
        }

        private static Color ClampOpaqueColor(Color color)
        {
            color.r = math.saturate(color.r);
            color.g = math.saturate(color.g);
            color.b = math.saturate(color.b);
            color.a = 1f;
            return color;
        }

        private static Color LerpColorClamped(Color from, Color to, float t)
        {
            float clampedT = math.saturate(t);
            return new Color(
                math.lerp(from.r, to.r, clampedT),
                math.lerp(from.g, to.g, clampedT),
                math.lerp(from.b, to.b, clampedT),
                math.lerp(from.a, to.a, clampedT));
        }

        private WeatherState ResolvePublishedMask()
        {
            WeatherState activeMask = ResolvePhaseMask(_activePhase);
            if (_biolumeSurgeTimer > 0f)
                activeMask |= WeatherState.BiolumeSurge;

            if (!_transitioning || _weatherIntensity >= 1f)
                return activeMask;

            WeatherState transitionMask = ResolvePhaseMask(_sourcePhase) | ResolvePhaseMask(_targetPhase);
            if (_biolumeSurgeTimer > 0f)
                transitionMask |= WeatherState.BiolumeSurge;

            return transitionMask;
        }

        private static WeatherState ResolvePhaseMask(WeatherPhase phase)
        {
            switch (phase)
            {
                case WeatherPhase.Storm:
                    return WeatherState.Storm | WeatherState.UpdraftActive;
                case WeatherPhase.CurrentSurge:
                    return WeatherState.Storm | WeatherState.ThermoclineActive | WeatherState.HaloclineActive;
                default:
                    return WeatherState.Calm;
            }
        }

        private static float3 ResolveDirectionalVector(Vector3 direction, float magnitude)
        {
            float3 dir = new float3(direction.x, direction.y, direction.z);
            return NormalizeSafe(dir, new float3(0f, 0f, 1f)) * math.max(0f, magnitude);
        }

        private static GerstnerWaveComponent ResolveWaveComponent(
            WaveBandAuthoring authoring,
            float amplitudeScale,
            float steepnessScale,
            float speedScale)
        {
            float2 direction = new float2(authoring.directionXZ.x, authoring.directionXZ.y);
            direction = NormalizeSafe(direction, new float2(1f, 0f));

            GerstnerWaveComponent component;
            component.DirectionXZ = direction;
            component.Amplitude = math.max(0f, authoring.amplitude * amplitudeScale);
            component.Wavelength = math.max(0.01f, authoring.wavelength);
            component.Steepness = math.max(0f, authoring.steepness * steepnessScale);
            component.PhaseOffset = authoring.phaseOffsetRadians;
            component.SpeedMultiplier = math.max(0.01f, authoring.speedMultiplier * speedScale);
            return component;
        }

        private PhaseProfile GetProfile(WeatherPhase phase)
        {
            switch (phase)
            {
                case WeatherPhase.Storm:
                    return stormProfile;
                case WeatherPhase.CurrentSurge:
                    return surgeProfile;
                default:
                    return calmProfile;
            }
        }

        private WeatherProfile GetWeatherProfile(WeatherPhase phase)
        {
            switch (phase)
            {
                case WeatherPhase.Storm:
                    return stormWeatherProfile;
                case WeatherPhase.CurrentSurge:
                    return surgeWeatherProfile;
                default:
                    return calmWeatherProfile;
            }
        }

        private float ResolveBaseWaveHeight()
        {
            return math.max(
                VectorNormalizeEpsilon,
                math.max(0f, wave0.amplitude) +
                math.max(0f, wave1.amplitude) +
                math.max(0f, wave2.amplitude));
        }

        private float ResolveWeatherProfileWaveHeight(WeatherProfile profile, float fallbackAmplitudeScale)
        {
            if (profile == null)
                return ResolveBaseWaveHeight() * math.max(0f, fallbackAmplitudeScale);

            return math.max(0f, profile.WaveHeightMax);
        }

        private static float ResolveWeatherProfileTurbulenceMultiplier(WeatherProfile profile)
        {
            return profile != null ? math.max(0f, profile.AbyssalTurbulenceMultiplier) : 1f;
        }

        private static float3 ResolveWeatherProfileWindVector(WeatherProfile profile, float3 fallbackWindVector)
        {
            if (profile == null)
                return fallbackWindVector;

            Vector3 authoredWind = profile.WindVector;
            float3 authoredWindFloat3 = new float3(authoredWind.x, authoredWind.y, authoredWind.z);
            return math.lengthsq(authoredWindFloat3) > VectorNormalizeEpsilon
                ? authoredWindFloat3
                : fallbackWindVector;
        }

        private static float ResolveExponentialBlendFactor(float deltaTime, float durationSeconds)
        {
            float clampedDeltaTime = math.max(0f, deltaTime);
            float duration = math.max(0.0001f, durationSeconds);
            return ApproximateOneMinusExpNegPositive((ExponentialBlendRateScale / duration) * clampedDeltaTime);
        }

        private float ResolveHoldDuration(PhaseProfile profile)
        {
            float min = math.max(1f, profile.minHoldSeconds);
            float max = math.max(min, profile.maxHoldSeconds);
            return math.lerp(min, max, NextRandom01());
        }

        private WeatherPhase ResolveNextPhase(WeatherPhase phase)
        {
            switch (phase)
            {
                case WeatherPhase.Storm:
                    return WeatherPhase.CurrentSurge;
                case WeatherPhase.CurrentSurge:
                    return WeatherPhase.Calm;
                default:
                    return WeatherPhase.Storm;
            }
        }

        private float ResolveSlowTickInterval()
        {
            return 0.5f;
        }

        private float NextRandom01()
        {
            if (_weatherRandomState == 0u)
                _weatherRandomState = 1u;

            _weatherRandomState = unchecked((_weatherRandomState * 1664525u) + 1013904223u);
            return (_weatherRandomState >> 8) * Lcg24BitToUnit;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (transitionDurationSeconds < 1f)
                transitionDurationSeconds = 1f;

            if (biomeBlendDurationSeconds < 0.25f)
                biomeBlendDurationSeconds = 0.25f;

            noirFogLutResolution = Mathf.Clamp(noirFogLutResolution, 8, MaxRuntimeNoirFogLutResolution);

            if (randomSeed == 0u)
                randomSeed = 1u;

            SanitizeProfile(ref calmProfile);
            SanitizeProfile(ref stormProfile);
            SanitizeProfile(ref surgeProfile);
            SanitizeWave(ref wave0);
            SanitizeWave(ref wave1);
            SanitizeWave(ref wave2);
            AssignDefaultProfilesIfMissing();
        }

        private void AssignDefaultProfilesIfMissing()
        {
            if (calmWeatherProfile == null)
                calmWeatherProfile = AssetDatabase.LoadAssetAtPath<WeatherProfile>(CalmWeatherProfileAssetPath);

            if (stormWeatherProfile == null)
                stormWeatherProfile = AssetDatabase.LoadAssetAtPath<WeatherProfile>(StormWeatherProfileAssetPath);

            if (surgeWeatherProfile == null)
                surgeWeatherProfile = AssetDatabase.LoadAssetAtPath<WeatherProfile>(SurgeWeatherProfileAssetPath);

            if (shallowLightBiomeProfile == null)
                shallowLightBiomeProfile = AssetDatabase.LoadAssetAtPath<WeatherProfile>(ShallowBiomeWeatherProfileAssetPath);

            if (midnightStillnessBiomeProfile == null)
                midnightStillnessBiomeProfile = AssetDatabase.LoadAssetAtPath<WeatherProfile>(MidnightBiomeWeatherProfileAssetPath);

            if (hadalsSurgeBiomeProfile == null)
                hadalsSurgeBiomeProfile = AssetDatabase.LoadAssetAtPath<WeatherProfile>(HadalBiomeWeatherProfileAssetPath);
        }
#endif

        private static void SanitizeProfile(ref PhaseProfile profile)
        {
            profile.windDirection = NormalizeSafe(profile.windDirection, Vector3.forward);

            profile.currentDirection = NormalizeSafe(profile.currentDirection, Vector3.forward);

            profile.windSpeed = math.max(0f, profile.windSpeed);
            profile.currentSpeed = math.max(0f, profile.currentSpeed);
            profile.thermalIntensity = math.max(0f, profile.thermalIntensity);
            profile.waveAmplitudeScale = math.max(0f, profile.waveAmplitudeScale);
            profile.waveSteepnessScale = math.max(0f, profile.waveSteepnessScale);
            profile.waveSpeedScale = math.max(0.01f, profile.waveSpeedScale);
            profile.minHoldSeconds = math.max(1f, profile.minHoldSeconds);
            profile.maxHoldSeconds = math.max(profile.minHoldSeconds, profile.maxHoldSeconds);
            profile.transitionProbability = math.saturate(profile.transitionProbability);
        }

        private static void SanitizeWave(ref WaveBandAuthoring band)
        {
            band.directionXZ = NormalizeSafe(band.directionXZ, Vector2.right);

            band.amplitude = math.max(0f, band.amplitude);
            band.wavelength = math.max(0.01f, band.wavelength);
            band.steepness = math.max(0f, band.steepness);
            band.speedMultiplier = math.max(0.01f, band.speedMultiplier);
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > VectorNormalizeEpsilon
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static float2 NormalizeSafe(float2 value, float2 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > VectorNormalizeEpsilon
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static Vector3 NormalizeSafe(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            return lengthSq > VectorNormalizeEpsilon
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static Vector2 NormalizeSafe(Vector2 value, Vector2 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            return lengthSq > VectorNormalizeEpsilon
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static float ApproximateMagnitude(float3 value)
        {
            float3 absValue = math.abs(value);
            float maxAxis = math.cmax(absValue);
            float minAxis = math.cmin(absValue);
            float midAxis = absValue.x + absValue.y + absValue.z - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.125f);
        }

        private static float ApproximateOneMinusExpNegPositive(float x)
        {
            return math.saturate(1f - ApproximateExpNegPositive(x));
        }

        private static float ApproximateExpNegPositive(float x)
        {
            float clamped = math.clamp(x, 0f, 8f);
            float x2 = clamped * clamped;
            float x3 = x2 * clamped;
            float numerator = 120f - (60f * clamped) + (12f * x2) - x3;
            float denominator = 120f + (60f * clamped) + (12f * x2) + x3;
            return math.saturate(numerator / math.max(denominator, 0.0001f));
        }
    }
}
