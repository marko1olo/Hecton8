using System;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using UnityEngine;
using Crest;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Atmosphere
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Atmosphere/Surface Weather Director")]
    [DefaultExecutionOrder(-4500)]
    public sealed class HectonSurfaceWeatherDirector : MonoBehaviour, ITickable, ISlowTickable
    {
        private const float ResolveRetryInterval = 2f;
        private const int ShelterSampleCount = 5;

        private enum SurfaceExecutionMode : byte
        {
            SurfaceActive = 0,
            SurfaceDormant = 1,
            SurfaceSuppressed = 2
        }

        private struct WeatherFrameState
        {
            public float cloudDensityThreshold;
            public float cloudSoftness;
            public float cloudSpeedMultiplier;
            public Vector2 windDirection;
            public float skyLuminanceMultiplier;
            public float starVisibilityMultiplier;
            public float stormEmissionMultiplier;
            public Color cloudLitColor;
            public Color cloudShadowColor;
            public Color sunsetCloudColor;
            public Color nightCloudColor;
            public Color surfaceFogColor;
            public float surfaceFogDensity;
            public Color surfaceAmbientColor;
            public float surfaceSunMultiplier;
            public float sunDiscMultiplier;
            public float sunScatterMultiplier;
            public float oceanWindSpeedKmh;
            public float oceanFoamStrength;
            public float oceanFoamCoverage;
            public float oceanFoamScale;
            public float precipitationIntensity;
            public float electricalActivity;
            public float lightningFlashIntensity;
            public float lightningFlashDuration;
            public float thunderDelayMin;
            public float thunderDelayMax;
            public float lightningStrikeDistanceMin;
            public float lightningStrikeDistanceMax;
            public float lightningWindBias;
            public float thunderPropagationDistanceScale;
            public float thunderVolumeNear;
            public float thunderVolumeFar;
            public float thunderPitchMin;
            public float thunderPitchMax;
            public float localRainAreaScale;
            public float localRainDensityMultiplier;
            public float surfaceImpactRadiusScale;
            public float surfaceImpactDensityMultiplier;
            public float lightningBoltWidthMultiplier;
            public float lightningLightRangeMultiplier;
            public float gustStrength;
            public float gustFrequency;
            public float squallStrength;
            public float squallFrequency;

            public static WeatherFrameState Lerp(in WeatherFrameState from, in WeatherFrameState to, float t)
            {
                return new WeatherFrameState
                {
                    cloudDensityThreshold = Mathf.Lerp(from.cloudDensityThreshold, to.cloudDensityThreshold, t),
                    cloudSoftness = Mathf.Lerp(from.cloudSoftness, to.cloudSoftness, t),
                    cloudSpeedMultiplier = Mathf.Lerp(from.cloudSpeedMultiplier, to.cloudSpeedMultiplier, t),
                    windDirection = Vector2.Lerp(from.windDirection, to.windDirection, t),
                    skyLuminanceMultiplier = Mathf.Lerp(from.skyLuminanceMultiplier, to.skyLuminanceMultiplier, t),
                    starVisibilityMultiplier = Mathf.Lerp(from.starVisibilityMultiplier, to.starVisibilityMultiplier, t),
                    stormEmissionMultiplier = Mathf.Lerp(from.stormEmissionMultiplier, to.stormEmissionMultiplier, t),
                    cloudLitColor = Color.Lerp(from.cloudLitColor, to.cloudLitColor, t),
                    cloudShadowColor = Color.Lerp(from.cloudShadowColor, to.cloudShadowColor, t),
                    sunsetCloudColor = Color.Lerp(from.sunsetCloudColor, to.sunsetCloudColor, t),
                    nightCloudColor = Color.Lerp(from.nightCloudColor, to.nightCloudColor, t),
                    surfaceFogColor = Color.Lerp(from.surfaceFogColor, to.surfaceFogColor, t),
                    surfaceFogDensity = Mathf.Lerp(from.surfaceFogDensity, to.surfaceFogDensity, t),
                    surfaceAmbientColor = Color.Lerp(from.surfaceAmbientColor, to.surfaceAmbientColor, t),
                    surfaceSunMultiplier = Mathf.Lerp(from.surfaceSunMultiplier, to.surfaceSunMultiplier, t),
                    sunDiscMultiplier = Mathf.Lerp(from.sunDiscMultiplier, to.sunDiscMultiplier, t),
                    sunScatterMultiplier = Mathf.Lerp(from.sunScatterMultiplier, to.sunScatterMultiplier, t),
                    oceanWindSpeedKmh = Mathf.Lerp(from.oceanWindSpeedKmh, to.oceanWindSpeedKmh, t),
                    oceanFoamStrength = Mathf.Lerp(from.oceanFoamStrength, to.oceanFoamStrength, t),
                    oceanFoamCoverage = Mathf.Lerp(from.oceanFoamCoverage, to.oceanFoamCoverage, t),
                    oceanFoamScale = Mathf.Lerp(from.oceanFoamScale, to.oceanFoamScale, t),
                    precipitationIntensity = Mathf.Lerp(from.precipitationIntensity, to.precipitationIntensity, t),
                    electricalActivity = Mathf.Lerp(from.electricalActivity, to.electricalActivity, t),
                    lightningFlashIntensity = Mathf.Lerp(from.lightningFlashIntensity, to.lightningFlashIntensity, t),
                    lightningFlashDuration = Mathf.Lerp(from.lightningFlashDuration, to.lightningFlashDuration, t),
                    thunderDelayMin = Mathf.Lerp(from.thunderDelayMin, to.thunderDelayMin, t),
                    thunderDelayMax = Mathf.Lerp(from.thunderDelayMax, to.thunderDelayMax, t),
                    lightningStrikeDistanceMin = Mathf.Lerp(from.lightningStrikeDistanceMin, to.lightningStrikeDistanceMin, t),
                    lightningStrikeDistanceMax = Mathf.Lerp(from.lightningStrikeDistanceMax, to.lightningStrikeDistanceMax, t),
                    lightningWindBias = Mathf.Lerp(from.lightningWindBias, to.lightningWindBias, t),
                    thunderPropagationDistanceScale = Mathf.Lerp(from.thunderPropagationDistanceScale, to.thunderPropagationDistanceScale, t),
                    thunderVolumeNear = Mathf.Lerp(from.thunderVolumeNear, to.thunderVolumeNear, t),
                    thunderVolumeFar = Mathf.Lerp(from.thunderVolumeFar, to.thunderVolumeFar, t),
                    thunderPitchMin = Mathf.Lerp(from.thunderPitchMin, to.thunderPitchMin, t),
                    thunderPitchMax = Mathf.Lerp(from.thunderPitchMax, to.thunderPitchMax, t),
                    localRainAreaScale = Mathf.Lerp(from.localRainAreaScale, to.localRainAreaScale, t),
                    localRainDensityMultiplier = Mathf.Lerp(from.localRainDensityMultiplier, to.localRainDensityMultiplier, t),
                    surfaceImpactRadiusScale = Mathf.Lerp(from.surfaceImpactRadiusScale, to.surfaceImpactRadiusScale, t),
                    surfaceImpactDensityMultiplier = Mathf.Lerp(from.surfaceImpactDensityMultiplier, to.surfaceImpactDensityMultiplier, t),
                    lightningBoltWidthMultiplier = Mathf.Lerp(from.lightningBoltWidthMultiplier, to.lightningBoltWidthMultiplier, t),
                    lightningLightRangeMultiplier = Mathf.Lerp(from.lightningLightRangeMultiplier, to.lightningLightRangeMultiplier, t),
                    gustStrength = Mathf.Lerp(from.gustStrength, to.gustStrength, t),
                    gustFrequency = Mathf.Lerp(from.gustFrequency, to.gustFrequency, t),
                    squallStrength = Mathf.Lerp(from.squallStrength, to.squallStrength, t),
                    squallFrequency = Mathf.Lerp(from.squallFrequency, to.squallFrequency, t)
                };
            }
        }

        private struct RuntimeWeatherProfile
        {
            public SurfaceWeatherKind kind;
            public float selectionWeight;
            public float minDurationSeconds;
            public float maxDurationSeconds;
            public WeatherFrameState state;
        }

        private struct LightningStrikePlan
        {
            public Vector3 impactPosition;
            public float phaseA;
            public float phaseB;
        }

        private static readonly int _idWaveFoamStrength = Shader.PropertyToID("_WaveFoamStrength");
        private static readonly int _idWaveFoamCoverage = Shader.PropertyToID("_WaveFoamCoverage");
        private static readonly int _idFoamScale = Shader.PropertyToID("_FoamScale");
        // COLD ALLOC: Vector2[5] - local shelter probe offsets around player for fractional rain exposure - owner: HectonSurfaceWeatherDirector
        private static readonly Vector2[] _shelterProbeOffsets =
        {
            Vector2.zero,
            new Vector2(1f, 0f),
            new Vector2(-1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, -1f)
        };

        private static HectonSurfaceWeatherDirector _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        [Header("Weather Profiles")]
        [Tooltip("Optional authored weather profiles. If empty, the built-in fallback library is used.")]
        [SerializeField] private SurfaceWeatherProfile[] weatherProfiles;

        [Tooltip("Optional thunder one-shots. Played as 3D ambient thunder through SpatialAudioManager.")]
        [SerializeField] private AudioClip[] thunderClips;

        [Header("Surface Activation")]
        [Tooltip("Depth in meters at or above which the full surface weather stack is considered active.")]
        [SerializeField, Min(0f)] private float surfaceActivationDepth = 0.03f;

        [Tooltip("Depth in meters below which the director starts counting toward suppressed mode.")]
        [SerializeField, Min(0f)] private float surfaceSuppressionDepth = 25f;

        [Tooltip("Seconds the player must remain deeper than the suppression depth before surface outputs are cleared.")]
        [SerializeField, Min(0.1f)] private float surfaceSuppressionDelay = 12f;

        [Header("Transition")]
        [Tooltip("Approximate seconds required to visually converge to the next weather target.")]
        [SerializeField, Min(1f)] private float weatherBlendDuration = 35f;

        [Header("Local Shelter")]
        [Tooltip("Layers treated as overhead rain blockers for the local player weather rig.")]
        [SerializeField] private LayerMask shelterOccluderMask = ~0;

        [Tooltip("Vertical offset from the follow target used as the shelter probe origin.")]
        [SerializeField, UnityEngine.Range(0.5f, 3f)] private float shelterProbeOriginOffset = 1.35f;

        [Tooltip("Maximum upward distance checked for local rain shelter such as roofs or dry modules.")]
        [SerializeField, Min(1f)] private float shelterProbeHeight = 12f;

        [Tooltip("Horizontal radius used for fractional local rain shelter probes around player.")]
        [SerializeField, Min(0.25f)] private float shelterProbeRadius = 2f;

        [Tooltip("Seconds used to visually converge local rain exposure after shelter state changes.")]
        [SerializeField, Min(0.05f)] private float shelterExposureBlendTime = 0.45f;

        [Header("References")]
        [Tooltip("Optional explicit player movement reference. If null, runtime resolve is used.")]
        [SerializeField] private HectonPlayerMovement playerMovement;

        [Tooltip("Optional explicit underwater visuals reference. If null, runtime resolve is used.")]
        [SerializeField] private HectonUnderwaterVisuals underwaterVisuals;

        [Tooltip("Optional explicit celestial engine reference. If null, runtime resolve is used.")]
        [SerializeField] private HectonCelestialEngine celestialEngine;

        [Tooltip("Optional explicit acoustic zone controller reference. If null, runtime resolve is used.")]
        [SerializeField] private AcousticZoneController acousticZoneController;

        [Tooltip("Optional explicit local weather VFX rig reference. If null, a scene-local rig is created on demand.")]
        [SerializeField] private SurfaceWeatherVfxRig weatherVfxRig;

        [Header("Diagnostics")]
        [SerializeField] private SurfaceWeatherKind _debugCurrentWeatherKind = SurfaceWeatherKind.ClearCalm;
        [SerializeField] private SurfaceExecutionMode _debugExecutionMode = SurfaceExecutionMode.SurfaceActive;
        [SerializeField] private float _debugCurrentDepth;
        [SerializeField] private float _debugHoldTimer;
        [SerializeField] private float _debugSuppressionTimer;
        [SerializeField] private float _debugLightningFlash;
        [SerializeField] private float _debugPrecipitation;
        [SerializeField] private float _debugElectricalActivity;
        [SerializeField] private float _debugGustMultiplier = 1f;
        [SerializeField] private float _debugSquallMultiplier = 1f;
        [SerializeField] private float _debugLocalRainExposure = 1f;
        [SerializeField] private bool _debugIsSheltered;

        // COLD ALLOC: RuntimeWeatherProfile[5] - built-in fallback weather library for scenes without authored assets - owner: HectonSurfaceWeatherDirector
        private readonly RuntimeWeatherProfile[] _fallbackProfiles = new RuntimeWeatherProfile[5];
        // COLD ALLOC: RaycastHit[8] - reusable shelter probe hits for local rain exposure checks - owner: HectonSurfaceWeatherDirector
        private readonly RaycastHit[] _shelterHits = new RaycastHit[8];
        // COLD ALLOC: List<GameObject>[16] - root traversal buffer for cold-path scene-owned manager resolve - owner: HectonSurfaceWeatherDirector
        private readonly System.Collections.Generic.List<GameObject> _sceneRootBuffer = new System.Collections.Generic.List<GameObject>(16);

        private RuntimeWeatherProfile _targetProfile;
        private WeatherFrameState _currentState;
        private SurfaceExecutionMode _executionMode;
        private uint _rngState = 1u;
        private float _weatherHoldTimer;
        private float _surfaceSuppressionTimer;
        private float _nextResolveTime;
        private float _lightningCooldown;
        private float _lightningFlashRemaining;
        private float _lightningFlashStrength;
        private float _pendingThunderDelay = -1f;
        private float _pendingThunderVolume;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _runtimeStateInitialized;
        private bool _bindingsApplied;
        private HectonPlayerMovement _subscribedPlayerMovement;
        private Transform _playerTransform;

        private OceanRenderer _oceanRenderer;
        private Material _oceanMaterial;
        private float _defaultOceanWindSpeed;
        private float _defaultFoamStrength = 1f;
        private float _defaultFoamCoverage = 1f;
        private float _defaultFoamScale = 1f;
        private bool _cachedOceanDefaults;
        private bool _hasFoamStrengthProperty;
        private bool _hasFoamCoverageProperty;
        private bool _hasFoamScaleProperty;
        private Vector3 _pendingThunderPosition;
        private float _pendingThunderPitch = 1f;
        private float _appliedOceanWindSpeed = float.MinValue;
        private float _appliedFoamStrength = float.MinValue;
        private float _appliedFoamCoverage = float.MinValue;
        private float _appliedFoamScale = float.MinValue;
        private float _gustTimeOffset;
        private float _currentLocalRainExposure = 1f;
        private float _targetLocalRainExposure = 1f;
        private bool _isLocallySheltered;

        /// <summary>
        /// Active surface weather director instance for the loaded world scene.
        /// </summary>
        public static HectonSurfaceWeatherDirector Instance => _instance;

        /// <summary>
        /// Weather family currently targeted by the director.
        /// </summary>
        public SurfaceWeatherKind CurrentWeatherKind => _targetProfile.kind;

        /// <summary>
        /// Current precipitation intensity after runtime blending.
        /// </summary>
        public float CurrentPrecipitationIntensity => _currentState.precipitationIntensity;

        /// <summary>
        /// Current electrical activity after runtime blending.
        /// </summary>
        public float CurrentElectricalActivity => _currentState.electricalActivity;

        /// <summary>
        /// Returns true while the player has been underwater long enough to suppress surface outputs.
        /// </summary>
        public bool IsSurfaceSuppressed => _executionMode == SurfaceExecutionMode.SurfaceSuppressed;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            BuildFallbackProfiles();
            SeedRandom();
#if UNITY_EDITOR
            TryAssignEditorAuthoringDefaults();
#endif
            TryResolveDependencies(true);
            InitializeRuntimeStateIfNeeded();
        }

        private void OnEnable()
        {
            TryRegisterTickManagers();
            TryResolveDependencies(true);
            RefreshPlayerMovementSubscription();
            InitializeRuntimeStateIfNeeded();
            SampleLocalRainExposure();
            _currentLocalRainExposure = _targetLocalRainExposure;
        }

        private void OnDisable()
        {
            TryUnregisterTickManagers();

            RefreshPlayerMovementSubscription(null);
            ClearWeatherBindings();
        }

        private void OnDestroy()
        {
            TryUnregisterTickManagers();
            RefreshPlayerMovementSubscription(null);

            if (_instance == this)
                _instance = null;
        }

        public void Tick(float deltaTime)
        {
            TryRegisterTickManagers();
            TryResolveDependencies(false);
            InitializeRuntimeStateIfNeeded();

            if (!_runtimeStateInitialized)
                return;

            UpdateExecutionMode(deltaTime);
            UpdateLocalRainExposure(deltaTime);
            UpdateWeatherSelection(deltaTime);
            UpdateLightningState(deltaTime);
            BlendWeather(deltaTime);
            ApplyWeatherBindings(deltaTime);
            UpdateDiagnostics();
        }

        public void SlowTick()
        {
            TryRegisterTickManagers();
            TryResolveDependencies(true);
            CacheOceanDefaults();
            InitializeRuntimeStateIfNeeded();
            SampleLocalRainExposure();
            UpdateDiagnostics();
        }

        private void TryRegisterTickManagers()
        {
            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            if (!_registeredTick)
            {
                tickManager.Register((ITickable)this);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                tickManager.Register((ISlowTickable)this);
                _registeredSlowTick = true;
            }
        }

        private void TryUnregisterTickManagers()
        {
            GameTickManager tickManager = GameTickManager.Instance;

            if (_registeredTick)
            {
                if (tickManager != null)
                    tickManager.Unregister((ITickable)this);

                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                if (tickManager != null)
                    tickManager.Unregister((ISlowTickable)this);

                _registeredSlowTick = false;
            }
        }

        private void TryResolveDependencies(bool force)
        {
            if (!Application.isPlaying)
                return;

            if (!force && Time.unscaledTime < _nextResolveTime)
                return;

            _nextResolveTime = Time.unscaledTime + ResolveRetryInterval;

            ResolvePlayerMovementReference();
            ResolveSceneOwnedReferences();

            if (_oceanRenderer == null)
                _oceanRenderer = OceanRenderer.Instance;

            if (weatherVfxRig == null)
                weatherVfxRig = GetComponentInChildren<SurfaceWeatherVfxRig>(true);

            if (weatherVfxRig == null)
                weatherVfxRig = CreateRuntimeVfxRig();

            RefreshPlayerMovementSubscription();
            CacheOceanDefaults();
        }

        private void ResolvePlayerMovementReference()
        {
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform currentPlayerTransform) &&
                currentPlayerTransform != null)
            {
                _playerTransform = currentPlayerTransform;

                if (playerMovement == null || playerMovement.transform != currentPlayerTransform)
                    currentPlayerTransform.TryGetComponent(out playerMovement);
            }
            else
            {
                _playerTransform = playerMovement != null ? playerMovement.transform : null;
            }
        }

        private void ResolveSceneOwnedReferences()
        {
            if (underwaterVisuals == null)
                TryGetComponent(out underwaterVisuals);

            if (acousticZoneController == null)
                acousticZoneController = AcousticZoneController.Instance;

            if (acousticZoneController == null)
                TryGetComponent(out acousticZoneController);

            if (weatherVfxRig == null)
                weatherVfxRig = GetComponentInChildren<SurfaceWeatherVfxRig>(true);

            if (celestialEngine != null)
                return;

            _sceneRootBuffer.Clear();
            gameObject.scene.GetRootGameObjects(_sceneRootBuffer);
            int rootCount = _sceneRootBuffer.Count;
            for (int i = 0; i < rootCount; i++)
            {
                GameObject rootObject = _sceneRootBuffer[i];
                if (rootObject == null)
                    continue;

                HectonCelestialEngine candidate = rootObject.GetComponentInChildren<HectonCelestialEngine>(true);
                if (candidate == null)
                    continue;

                celestialEngine = candidate;
                break;
            }
        }

        private SurfaceWeatherVfxRig CreateRuntimeVfxRig()
        {
            if (!Application.isPlaying)
                return null;

            // COLD ALLOC: GameObject[1] - scene-local weather VFX rig for rain and lightning - owner: HectonSurfaceWeatherDirector
            GameObject rigRoot = new GameObject("SurfaceWeatherVfxRig");
            rigRoot.transform.SetParent(transform, false);
            return rigRoot.AddComponent<SurfaceWeatherVfxRig>();
        }

        private void RefreshPlayerMovementSubscription()
        {
            RefreshPlayerMovementSubscription(playerMovement);
        }

        private void RefreshPlayerMovementSubscription(HectonPlayerMovement target)
        {
            if (_subscribedPlayerMovement == target)
                return;

            if (_subscribedPlayerMovement != null)
                _subscribedPlayerMovement.OnWaterSplash -= HandlePlayerWaterSplash;

            _subscribedPlayerMovement = target;
            _playerTransform = _subscribedPlayerMovement != null ? _subscribedPlayerMovement.transform : null;

            if (_subscribedPlayerMovement != null)
                _subscribedPlayerMovement.OnWaterSplash += HandlePlayerWaterSplash;
        }

        private void CacheOceanDefaults()
        {
            if (_cachedOceanDefaults)
                return;

            if (_oceanRenderer == null)
                _oceanRenderer = OceanRenderer.Instance;

            if (_oceanRenderer == null)
                return;

            _oceanMaterial = _oceanRenderer.OceanMaterial;
            _defaultOceanWindSpeed = Mathf.Max(0f, _oceanRenderer._globalWindSpeed);

            if (_oceanMaterial != null)
            {
                _hasFoamStrengthProperty = _oceanMaterial.HasProperty(_idWaveFoamStrength);
                _hasFoamCoverageProperty = _oceanMaterial.HasProperty(_idWaveFoamCoverage);
                _hasFoamScaleProperty = _oceanMaterial.HasProperty(_idFoamScale);

                if (_hasFoamStrengthProperty)
                    _defaultFoamStrength = _oceanMaterial.GetFloat(_idWaveFoamStrength);

                if (_hasFoamCoverageProperty)
                    _defaultFoamCoverage = _oceanMaterial.GetFloat(_idWaveFoamCoverage);

                if (_hasFoamScaleProperty)
                    _defaultFoamScale = _oceanMaterial.GetFloat(_idFoamScale);
            }

            _cachedOceanDefaults = true;
        }

        private void InitializeRuntimeStateIfNeeded()
        {
            if (_runtimeStateInitialized)
                return;

            RuntimeWeatherProfile initialProfile;
            if (!TrySelectInitialProfile(out initialProfile))
                return;

            _targetProfile = initialProfile;
            _currentState = initialProfile.state;
            _weatherHoldTimer = ResolveProfileHoldDuration(initialProfile);
            _lightningCooldown = ResolveNextLightningCooldown(_currentState.electricalActivity);
            _runtimeStateInitialized = true;
            _debugCurrentWeatherKind = initialProfile.kind;
            SampleLocalRainExposure();
            _currentLocalRainExposure = _targetLocalRainExposure;
        }

        private void UpdateExecutionMode(float deltaTime)
        {
            float currentDepth = ResolveCurrentDepth();
            _debugCurrentDepth = currentDepth;

            if (currentDepth <= surfaceActivationDepth)
            {
                _surfaceSuppressionTimer = 0f;
                _executionMode = SurfaceExecutionMode.SurfaceActive;
                return;
            }

            if (currentDepth >= surfaceSuppressionDepth)
            {
                _surfaceSuppressionTimer += deltaTime;
                if (_surfaceSuppressionTimer >= surfaceSuppressionDelay)
                {
                    _executionMode = SurfaceExecutionMode.SurfaceSuppressed;
                    return;
                }
            }
            else
            {
                _surfaceSuppressionTimer = 0f;
            }

            _executionMode = SurfaceExecutionMode.SurfaceDormant;
        }

        private float ResolveCurrentDepth()
        {
            if (playerMovement == null)
                return 0f;

            return Mathf.Max(0f, playerMovement.CurrentDepth);
        }

        private void HandlePlayerWaterSplash(float intensity)
        {
            if (weatherVfxRig == null || _executionMode != SurfaceExecutionMode.SurfaceActive)
                return;

            float clampedIntensity = Mathf.Clamp01(intensity * Mathf.Lerp(0.85f, 1.2f, _currentState.precipitationIntensity));
            if (clampedIntensity <= 0.01f)
                return;

            Vector3 followPosition = ResolveFollowPosition();
            float surfaceY = ResolveSurfaceY(followPosition);
            weatherVfxRig.TriggerSurfaceSplashBurst(
                followPosition,
                surfaceY,
                _currentState.windDirection,
                clampedIntensity);
        }

        private Vector3 ResolveFollowPosition()
        {
            return _playerTransform != null ? _playerTransform.position : transform.position;
        }

        private float ResolveSurfaceY(Vector3 followPosition)
        {
            return playerMovement != null ? playerMovement.CurrentWaterSurfaceY : followPosition.y;
        }

        private void UpdateWeatherSelection(float deltaTime)
        {
            _weatherHoldTimer -= deltaTime;
            if (_weatherHoldTimer > 0f)
                return;

            RuntimeWeatherProfile nextProfile;
            if (!TrySelectNextProfile(_targetProfile.kind, out nextProfile))
            {
                _weatherHoldTimer = ResolveProfileHoldDuration(_targetProfile);
                return;
            }

            _targetProfile = nextProfile;
            _weatherHoldTimer = ResolveProfileHoldDuration(nextProfile);
            _debugCurrentWeatherKind = nextProfile.kind;
        }

        private void UpdateLightningState(float deltaTime)
        {
            if (_lightningFlashRemaining > 0f)
            {
                _lightningFlashRemaining -= deltaTime;
                if (_lightningFlashRemaining <= 0f)
                {
                    _lightningFlashRemaining = 0f;
                    _lightningFlashStrength = 0f;
                }
            }

            if (_pendingThunderDelay >= 0f)
            {
                _pendingThunderDelay -= deltaTime;
                if (_pendingThunderDelay <= 0f)
                {
                    PlayThunder();
                    _pendingThunderDelay = -1f;
                }
            }

            float electricalActivity = Mathf.Clamp01(_currentState.electricalActivity);
            if (electricalActivity <= 0.2f)
                return;

            _lightningCooldown -= deltaTime;
            if (_lightningCooldown > 0f)
                return;

            TriggerLightning(electricalActivity);
        }

        private void TriggerLightning(float electricalActivity)
        {
            float flashDuration = Mathf.Max(0.02f, _currentState.lightningFlashDuration);
            float flashBase = Mathf.Max(0f, _currentState.lightningFlashIntensity);
            float flashVariance = Mathf.Lerp(0.7f, 1f, NextRandom01());
            float gustMultiplier = ResolveGustMultiplier(_currentState);
            float strikeRandomA = NextRandom01();
            float strikeRandomB = NextRandom01();
            Vector3 followPosition = ResolveFollowPosition();
            float surfaceY = ResolveSurfaceY(followPosition);
            LightningStrikePlan strikePlan = ResolveLightningStrikePlan(
                followPosition,
                surfaceY,
                _currentState.windDirection,
                strikeRandomA,
                strikeRandomB);

            _lightningFlashRemaining = flashDuration;
            _lightningFlashStrength = flashBase * flashVariance;
            _lightningCooldown = ResolveNextLightningCooldown(electricalActivity);

            if (weatherVfxRig != null &&
                _executionMode == SurfaceExecutionMode.SurfaceActive)
            {
                weatherVfxRig.TriggerLightningStrike(
                    strikePlan.impactPosition,
                    _currentState.windDirection,
                    _lightningFlashStrength,
                    strikePlan.phaseA,
                    strikePlan.phaseB,
                    _currentState.lightningBoltWidthMultiplier,
                    _currentState.lightningLightRangeMultiplier * Mathf.Lerp(1f, gustMultiplier, 0.08f));
            }

            if (thunderClips != null && thunderClips.Length > 0)
            {
                ConfigurePendingThunder(strikePlan.impactPosition, followPosition, electricalActivity);
            }
            else
            {
                _pendingThunderDelay = -1f;
            }
        }

        private LightningStrikePlan ResolveLightningStrikePlan(
            Vector3 followPosition,
            float surfaceY,
            Vector2 windDirection,
            float randomA,
            float randomB)
        {
            Vector2 preferredDirection = windDirection;
            if (preferredDirection.sqrMagnitude < 0.0001f)
                preferredDirection = Vector2.right;
            else
                preferredDirection.Normalize();

            float randomAngle = randomA * Mathf.PI * 2f;
            Vector2 randomDirection = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
            float clampedWindBias = Mathf.Clamp01(_currentState.lightningWindBias);
            float angularOffset = ((randomB * 2f) - 1f) * Mathf.Lerp(1.4f, 0.35f, clampedWindBias);
            Vector2 windBiasedDirection = RotateDirection(preferredDirection, angularOffset);
            Vector2 resolvedDirection = Vector2.Lerp(randomDirection, windBiasedDirection, clampedWindBias);
            if (resolvedDirection.sqrMagnitude < 0.0001f)
                resolvedDirection = randomDirection;

            resolvedDirection.Normalize();

            float minDistance = Mathf.Max(10f, _currentState.lightningStrikeDistanceMin);
            float maxDistance = Mathf.Max(minDistance, _currentState.lightningStrikeDistanceMax);
            float distance = Mathf.Lerp(minDistance, maxDistance, randomA);

            LightningStrikePlan plan;
            plan.impactPosition = followPosition + new Vector3(resolvedDirection.x, 0f, resolvedDirection.y) * distance;
            plan.impactPosition.y = surfaceY;
            plan.phaseA = randomA;
            plan.phaseB = randomB;
            return plan;
        }

        private void ConfigurePendingThunder(Vector3 strikePosition, Vector3 listenerPosition, float electricalActivity)
        {
            float thunderDistance = Vector3.Distance(listenerPosition, strikePosition);
            float minDistance = Mathf.Max(10f, _currentState.lightningStrikeDistanceMin);
            float maxDistance = Mathf.Max(minDistance, _currentState.lightningStrikeDistanceMax);
            float distanceT = Mathf.InverseLerp(minDistance, maxDistance, thunderDistance);
            float loudness = Mathf.Lerp(_currentState.thunderVolumeNear, _currentState.thunderVolumeFar, distanceT);
            float stormBoost = Mathf.Lerp(0.65f, 1f, electricalActivity);
            float effectiveDistance = thunderDistance * Mathf.Max(0.25f, _currentState.thunderPropagationDistanceScale);
            float thunderDelay = effectiveDistance / 343f;

            _pendingThunderPosition = strikePosition;
            _pendingThunderDelay = Mathf.Clamp(
                thunderDelay,
                _currentState.thunderDelayMin,
                _currentState.thunderDelayMax);
            _pendingThunderVolume = loudness * stormBoost;
            _pendingThunderPitch = Mathf.Lerp(
                _currentState.thunderPitchMin,
                _currentState.thunderPitchMax,
                NextRandom01()) * Mathf.Lerp(0.94f, 1.02f, 1f - distanceT);
        }

        private static Vector2 RotateDirection(Vector2 direction, float angleRadians)
        {
            float sin = Mathf.Sin(angleRadians);
            float cos = Mathf.Cos(angleRadians);
            return new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos);
        }

        private float ResolveNextLightningCooldown(float electricalActivity)
        {
            float clampedElectrical = Mathf.Clamp01(electricalActivity);
            float baseInterval = Mathf.Lerp(18f, 4.5f, clampedElectrical);
            float jitter = Mathf.Lerp(8f, 1.5f, clampedElectrical);
            return Mathf.Max(0.5f, baseInterval + ((NextRandom01() * 2f) - 1f) * jitter);
        }

        private void BlendWeather(float deltaTime)
        {
            float blendT = Mathf.Clamp01(deltaTime / weatherBlendDuration);
            _currentState = WeatherFrameState.Lerp(_currentState, _targetProfile.state, blendT);
        }

        private void UpdateDiagnostics()
        {
            _debugExecutionMode = _executionMode;
            _debugHoldTimer = _weatherHoldTimer;
            _debugSuppressionTimer = _surfaceSuppressionTimer;
            _debugLightningFlash = _lightningFlashStrength;
            _debugPrecipitation = _currentState.precipitationIntensity;
            _debugElectricalActivity = _currentState.electricalActivity;
            _debugGustMultiplier = ResolveGustMultiplier(_currentState);
            _debugSquallMultiplier = ResolveSquallMultiplier(_currentState);
            _debugLocalRainExposure = _currentLocalRainExposure;
            _debugIsSheltered = _isLocallySheltered;
        }

        private void UpdateLocalRainExposure(float deltaTime)
        {
            float blendTime = Mathf.Max(0.05f, shelterExposureBlendTime);
            float blendT = Mathf.Clamp01(deltaTime / blendTime);
            _currentLocalRainExposure = Mathf.Lerp(_currentLocalRainExposure, _targetLocalRainExposure, blendT);
        }

        private void SampleLocalRainExposure()
        {
            if (!_runtimeStateInitialized)
                return;

            if (_executionMode == SurfaceExecutionMode.SurfaceSuppressed)
            {
                _targetLocalRainExposure = 0f;
                _isLocallySheltered = true;
                return;
            }

            if (ResolveCurrentDepth() > surfaceActivationDepth)
            {
                _targetLocalRainExposure = 0f;
                _isLocallySheltered = true;
                return;
            }

            if (acousticZoneController != null && acousticZoneController.IsInterior)
            {
                _targetLocalRainExposure = 0f;
                _isLocallySheltered = true;
                return;
            }

            if (_playerTransform == null)
            {
                _targetLocalRainExposure = 1f;
                _isLocallySheltered = false;
                return;
            }

            Vector3 followPosition = ResolveFollowPosition();
            float probeRadius = Mathf.Max(0.25f, shelterProbeRadius);
            int openProbeCount = 0;

            for (int i = 0; i < ShelterSampleCount; i++)
            {
                Vector2 sampleOffset = _shelterProbeOffsets[i] * probeRadius;
                Vector3 probeOrigin = followPosition;
                probeOrigin.x += sampleOffset.x;
                probeOrigin.z += sampleOffset.y;
                probeOrigin.y += shelterProbeOriginOffset;

                if (!IsShelterProbeBlocked(probeOrigin))
                    openProbeCount++;
            }

            _targetLocalRainExposure = openProbeCount / (float)ShelterSampleCount;
            _isLocallySheltered = _targetLocalRainExposure < 0.999f;
        }

        private bool IsShelterProbeBlocked(Vector3 probeOrigin)
        {
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                probeOrigin,
                Vector3.up,
                _shelterHits,
                shelterProbeHeight,
                shelterOccluderMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = _shelterHits[i].collider;
                if (hitCollider == null)
                    continue;

                Transform hitTransform = hitCollider.transform;
                if (_playerTransform != null && hitTransform != null && hitTransform.IsChildOf(_playerTransform))
                    continue;

                return true;
            }

            return false;
        }

        private float ResolveGustMultiplier(in WeatherFrameState state)
        {
            float gustStrength = Mathf.Clamp01(state.gustStrength);
            if (gustStrength <= 0.001f)
                return 1f;

            float frequency = Mathf.Clamp(state.gustFrequency, 0.005f, 0.2f);
            float phase = (Time.unscaledTime + _gustTimeOffset) * frequency * Mathf.PI * 2f;
            float composite =
                Mathf.Sin(phase) * 0.58f +
                Mathf.Sin(phase * 0.43f + 1.17f) * 0.29f +
                Mathf.Sin(phase * 1.73f + 0.41f) * 0.13f;

            float normalized = Mathf.Clamp01((composite + 1f) * 0.5f);
            float envelope = normalized * normalized;
            float calmFloor = 1f - gustStrength * 0.12f;
            float gustPeak = 1f + gustStrength * 0.42f;
            return Mathf.Lerp(calmFloor, gustPeak, envelope);
        }

        private float ResolveSquallMultiplier(in WeatherFrameState state)
        {
            float squallStrength = Mathf.Clamp01(state.squallStrength);
            if (squallStrength <= 0.001f)
                return 1f;

            float frequency = Mathf.Clamp(state.squallFrequency, 0.005f, 0.08f);
            float phase = (Time.unscaledTime + _gustTimeOffset * 0.37f) * frequency * Mathf.PI * 2f;
            float composite =
                Mathf.Sin(phase) * 0.61f +
                Mathf.Sin(phase * 0.31f + 2.14f) * 0.27f +
                Mathf.Sin(phase * 1.09f + 0.63f) * 0.12f;

            float normalized = Mathf.Clamp01((composite + 1f) * 0.5f);
            float bandEnvelope = normalized * normalized * normalized;
            float calmFloor = 1f - squallStrength * 0.26f;
            float squallPeak = 1f + squallStrength * 0.72f;
            return Mathf.Lerp(calmFloor, squallPeak, bandEnvelope);
        }

        private void ApplyWeatherBindings(float deltaTime)
        {
            if (_executionMode == SurfaceExecutionMode.SurfaceSuppressed)
            {
                ClearWeatherBindings();
                return;
            }

            float flashStrength = Mathf.Max(0f, _lightningFlashStrength);
            float gustMultiplier = ResolveGustMultiplier(_currentState);
            float squallMultiplier = ResolveSquallMultiplier(_currentState);
            bool surfaceVfxActive = _executionMode == SurfaceExecutionMode.SurfaceActive;
            float localRainExposure = surfaceVfxActive ? Mathf.Clamp01(_currentLocalRainExposure) : 0f;
            float skyLuminance = Mathf.Max(0f, _currentState.skyLuminanceMultiplier + flashStrength * 0.22f);
            float sunDisc = Mathf.Max(0f, _currentState.sunDiscMultiplier + flashStrength);
            float sunScatter = Mathf.Max(0f, _currentState.sunScatterMultiplier + flashStrength * 0.35f);
            float sunMultiplier = Mathf.Max(0f, _currentState.surfaceSunMultiplier + flashStrength * 0.45f);
            float cloudSpeedMultiplier = _currentState.cloudSpeedMultiplier * Mathf.Lerp(1f, gustMultiplier, 0.35f);
            float vfxPrecipitation = surfaceVfxActive
                ? Mathf.Clamp01(_currentState.precipitationIntensity * Mathf.Lerp(1f, gustMultiplier, 0.4f) * squallMultiplier * localRainExposure)
                : 0f;
            float acousticPrecipitation = Mathf.Clamp01(_currentState.precipitationIntensity * squallMultiplier);
            float localRainAreaScale = _currentState.localRainAreaScale * Mathf.Lerp(1f, gustMultiplier, 0.18f) * Mathf.Lerp(1f, squallMultiplier, 0.08f);
            float localRainDensityMultiplier = _currentState.localRainDensityMultiplier * Mathf.Lerp(1f, gustMultiplier, 0.35f) * squallMultiplier * Mathf.Lerp(0.4f, 1f, localRainExposure);
            float surfaceImpactRadiusScale = _currentState.surfaceImpactRadiusScale * Mathf.Lerp(1f, gustMultiplier, 0.12f) * Mathf.Lerp(1f, squallMultiplier, 0.15f);
            float surfaceImpactDensityMultiplier = _currentState.surfaceImpactDensityMultiplier * Mathf.Lerp(1f, gustMultiplier, 0.42f) * squallMultiplier * localRainExposure;

            if (underwaterVisuals != null)
            {
                underwaterVisuals.SetSurfaceWeatherOverride(
                    _currentState.surfaceFogColor,
                    _currentState.surfaceFogDensity,
                    _currentState.surfaceAmbientColor,
                    sunMultiplier);
            }

            if (celestialEngine != null)
            {
                celestialEngine.SetSurfaceWeatherOverride(
                    _currentState.cloudDensityThreshold,
                    _currentState.cloudSoftness,
                    cloudSpeedMultiplier,
                    _currentState.windDirection,
                    _currentState.surfaceFogColor,
                    _currentState.surfaceFogDensity,
                    _currentState.surfaceAmbientColor,
                    sunMultiplier,
                    _currentState.starVisibilityMultiplier,
                    _currentState.stormEmissionMultiplier,
                    skyLuminance,
                    sunDisc,
                    sunScatter,
                    _currentState.cloudLitColor,
                    _currentState.cloudShadowColor,
                    _currentState.sunsetCloudColor,
                    _currentState.nightCloudColor);
            }

            if (acousticZoneController != null)
            {
                acousticZoneController.SetSurfaceWeatherMix(
                    acousticPrecipitation,
                    _currentState.electricalActivity);
            }

            if (weatherVfxRig != null)
            {
                Vector3 followPosition = ResolveFollowPosition();
                float surfaceY = ResolveSurfaceY(followPosition);
                weatherVfxRig.ApplyState(
                    deltaTime,
                    followPosition,
                    surfaceY,
                    _currentState.windDirection,
                    vfxPrecipitation,
                    localRainAreaScale,
                    localRainDensityMultiplier,
                    surfaceImpactRadiusScale,
                    surfaceImpactDensityMultiplier,
                    surfaceVfxActive);
            }

            _debugGustMultiplier = gustMultiplier;
            _debugSquallMultiplier = squallMultiplier;
            ApplyOceanState(_currentState, gustMultiplier);
            _bindingsApplied = true;
        }

        private void ClearWeatherBindings()
        {
            if (!_bindingsApplied)
                return;

            if (underwaterVisuals != null)
                underwaterVisuals.ClearSurfaceWeatherOverride();

            if (celestialEngine != null)
                celestialEngine.ClearSurfaceWeatherOverride();

            if (acousticZoneController != null)
                acousticZoneController.ClearSurfaceWeatherMix();

            if (weatherVfxRig != null)
                weatherVfxRig.ClearState();

            _pendingThunderDelay = -1f;
            _pendingThunderVolume = 0f;
            _pendingThunderPitch = 1f;
            RestoreOceanDefaults();
            _bindingsApplied = false;
        }

        private void ApplyOceanState(in WeatherFrameState state, float gustMultiplier)
        {
            const float OceanPropertyEpsilon = 0.01f;

            if (_oceanRenderer == null)
                _oceanRenderer = OceanRenderer.Instance;

            if (_oceanRenderer == null)
                return;

            CacheOceanDefaults();
            float targetWindSpeed = Mathf.Max(0f, state.oceanWindSpeedKmh * Mathf.Lerp(1f, gustMultiplier, 0.42f));
            if (Mathf.Abs(_appliedOceanWindSpeed - targetWindSpeed) > OceanPropertyEpsilon)
            {
                _oceanRenderer._globalWindSpeed = targetWindSpeed;
                _appliedOceanWindSpeed = targetWindSpeed;
            }

            Material oceanMaterial = _oceanRenderer.OceanMaterial;
            if (oceanMaterial == null)
                return;

            float targetFoamStrength = _defaultFoamStrength * Mathf.Max(0f, state.oceanFoamStrength * Mathf.Lerp(1f, gustMultiplier, 0.24f));
            float targetFoamCoverage = _defaultFoamCoverage * Mathf.Max(0f, state.oceanFoamCoverage * Mathf.Lerp(1f, gustMultiplier, 0.18f));
            float targetFoamScale = _defaultFoamScale * Mathf.Max(0.1f, state.oceanFoamScale * Mathf.Lerp(1f, gustMultiplier, 0.08f));

            if (_hasFoamStrengthProperty)
            {
                if (Mathf.Abs(_appliedFoamStrength - targetFoamStrength) > OceanPropertyEpsilon)
                {
                    oceanMaterial.SetFloat(_idWaveFoamStrength, targetFoamStrength);
                    _appliedFoamStrength = targetFoamStrength;
                }
            }

            if (_hasFoamCoverageProperty)
            {
                if (Mathf.Abs(_appliedFoamCoverage - targetFoamCoverage) > OceanPropertyEpsilon)
                {
                    oceanMaterial.SetFloat(_idWaveFoamCoverage, targetFoamCoverage);
                    _appliedFoamCoverage = targetFoamCoverage;
                }
            }

            if (_hasFoamScaleProperty)
            {
                if (Mathf.Abs(_appliedFoamScale - targetFoamScale) > OceanPropertyEpsilon)
                {
                    oceanMaterial.SetFloat(_idFoamScale, targetFoamScale);
                    _appliedFoamScale = targetFoamScale;
                }
            }
        }

        private void RestoreOceanDefaults()
        {
            if (!_cachedOceanDefaults)
                return;

            if (_oceanRenderer == null)
                _oceanRenderer = OceanRenderer.Instance;

            if (_oceanRenderer == null)
                return;

            _oceanRenderer._globalWindSpeed = _defaultOceanWindSpeed;

            Material oceanMaterial = _oceanRenderer.OceanMaterial;
            if (oceanMaterial == null)
                return;

            if (_hasFoamStrengthProperty)
                oceanMaterial.SetFloat(_idWaveFoamStrength, _defaultFoamStrength);

            if (_hasFoamCoverageProperty)
                oceanMaterial.SetFloat(_idWaveFoamCoverage, _defaultFoamCoverage);

            if (_hasFoamScaleProperty)
                oceanMaterial.SetFloat(_idFoamScale, _defaultFoamScale);

            _appliedOceanWindSpeed = _defaultOceanWindSpeed;
            _appliedFoamStrength = _defaultFoamStrength;
            _appliedFoamCoverage = _defaultFoamCoverage;
            _appliedFoamScale = _defaultFoamScale;
        }

        private void PlayThunder()
        {
            if (thunderClips == null || thunderClips.Length == 0)
                return;

            if (!SpatialAudioManager.TryGetInstance(out SpatialAudioManager audioManager) || audioManager == null)
                return;

            int clipIndex = Mathf.Clamp((int)(NextRandom01() * thunderClips.Length), 0, thunderClips.Length - 1);
            AudioClip clip = thunderClips[clipIndex];
            if (clip == null)
                return;

            audioManager.PlayAtPoint(
                clip,
                _pendingThunderPosition,
                _pendingThunderVolume,
                _pendingThunderPitch,
                audioManager.AmbientGroup);
        }

        private bool TrySelectInitialProfile(out RuntimeWeatherProfile profile)
        {
            return TrySelectWeightedProfile(SurfaceWeatherKind.ClearCalm, false, out profile);
        }

        private bool TrySelectNextProfile(SurfaceWeatherKind currentKind, out RuntimeWeatherProfile profile)
        {
            return TrySelectWeightedProfile(currentKind, true, out profile);
        }

        private bool TrySelectWeightedProfile(
            SurfaceWeatherKind excludedKind,
            bool excludeCurrentKind,
            out RuntimeWeatherProfile profile)
        {
            profile = default;
            float totalWeight = 0f;
            int profileCount = GetProfileCount();

            for (int i = 0; i < profileCount; i++)
            {
                RuntimeWeatherProfile candidate;
                if (!TryGetProfileAt(i, out candidate))
                    continue;

                if (excludeCurrentKind && candidate.kind == excludedKind && profileCount > 1)
                    continue;

                totalWeight += Mathf.Max(0.001f, candidate.selectionWeight);
            }

            if (totalWeight <= 0f)
                return TryGetProfileAt(0, out profile);

            float selection = NextRandom01() * totalWeight;
            float accumulator = 0f;
            for (int i = 0; i < profileCount; i++)
            {
                RuntimeWeatherProfile candidate;
                if (!TryGetProfileAt(i, out candidate))
                    continue;

                if (excludeCurrentKind && candidate.kind == excludedKind && profileCount > 1)
                    continue;

                accumulator += Mathf.Max(0.001f, candidate.selectionWeight);
                if (selection <= accumulator)
                {
                    profile = candidate;
                    return true;
                }
            }

            return TryGetProfileAt(0, out profile);
        }

        private float ResolveProfileHoldDuration(in RuntimeWeatherProfile profile)
        {
            if (profile.maxDurationSeconds <= profile.minDurationSeconds)
                return profile.minDurationSeconds;

            return Mathf.Lerp(profile.minDurationSeconds, profile.maxDurationSeconds, NextRandom01());
        }

        private int GetProfileCount()
        {
            int authoredCount = 0;
            if (weatherProfiles != null)
            {
                for (int i = 0; i < weatherProfiles.Length; i++)
                {
                    if (weatherProfiles[i] != null)
                        authoredCount++;
                }
            }

            return authoredCount > 0 ? authoredCount : _fallbackProfiles.Length;
        }

        private bool TryGetProfileAt(int index, out RuntimeWeatherProfile profile)
        {
            if (weatherProfiles != null)
            {
                int authoredIndex = 0;
                for (int i = 0; i < weatherProfiles.Length; i++)
                {
                    SurfaceWeatherProfile asset = weatherProfiles[i];
                    if (asset == null)
                        continue;

                    if (authoredIndex == index)
                    {
                        profile = ConvertAssetProfile(asset);
                        return true;
                    }

                    authoredIndex++;
                }
            }

            if (index >= 0 && index < _fallbackProfiles.Length)
            {
                profile = _fallbackProfiles[index];
                return true;
            }

            profile = default;
            return false;
        }

        private static RuntimeWeatherProfile ConvertAssetProfile(SurfaceWeatherProfile asset)
        {
            RuntimeWeatherProfile profile;
            profile.kind = asset.WeatherKind;
            profile.selectionWeight = asset.SelectionWeight;
            profile.minDurationSeconds = asset.MinDurationSeconds;
            profile.maxDurationSeconds = asset.MaxDurationSeconds;
            profile.state = new WeatherFrameState
            {
                cloudDensityThreshold = asset.CloudDensityThreshold,
                cloudSoftness = asset.CloudSoftness,
                cloudSpeedMultiplier = asset.CloudSpeedMultiplier,
                windDirection = asset.WindDirection,
                skyLuminanceMultiplier = asset.SkyLuminanceMultiplier,
                starVisibilityMultiplier = asset.StarVisibilityMultiplier,
                stormEmissionMultiplier = asset.StormEmissionMultiplier,
                cloudLitColor = asset.CloudLitColor,
                cloudShadowColor = asset.CloudShadowColor,
                sunsetCloudColor = asset.SunsetCloudColor,
                nightCloudColor = asset.NightCloudColor,
                surfaceFogColor = asset.SurfaceFogColor,
                surfaceFogDensity = asset.SurfaceFogDensity,
                surfaceAmbientColor = asset.SurfaceAmbientColor,
                surfaceSunMultiplier = asset.SurfaceSunMultiplier,
                sunDiscMultiplier = asset.SunDiscMultiplier,
                sunScatterMultiplier = asset.SunScatterMultiplier,
                oceanWindSpeedKmh = asset.OceanWindSpeedKmh,
                oceanFoamStrength = asset.OceanFoamStrength,
                oceanFoamCoverage = asset.OceanFoamCoverage,
                oceanFoamScale = asset.OceanFoamScale,
                precipitationIntensity = asset.PrecipitationIntensity,
                electricalActivity = asset.ElectricalActivity,
                lightningFlashIntensity = asset.LightningFlashIntensity,
                lightningFlashDuration = asset.LightningFlashDuration,
                thunderDelayMin = asset.ThunderDelayMin,
                thunderDelayMax = asset.ThunderDelayMax,
                lightningStrikeDistanceMin = asset.LightningStrikeDistanceMin,
                lightningStrikeDistanceMax = asset.LightningStrikeDistanceMax,
                lightningWindBias = asset.LightningWindBias,
                thunderPropagationDistanceScale = asset.ThunderPropagationDistanceScale,
                thunderVolumeNear = asset.ThunderVolumeNear,
                thunderVolumeFar = asset.ThunderVolumeFar,
                thunderPitchMin = asset.ThunderPitchMin,
                thunderPitchMax = asset.ThunderPitchMax,
                localRainAreaScale = asset.LocalRainAreaScale,
                localRainDensityMultiplier = asset.LocalRainDensityMultiplier,
                surfaceImpactRadiusScale = asset.SurfaceImpactRadiusScale,
                surfaceImpactDensityMultiplier = asset.SurfaceImpactDensityMultiplier,
                lightningBoltWidthMultiplier = asset.LightningBoltWidthMultiplier,
                lightningLightRangeMultiplier = asset.LightningLightRangeMultiplier,
                gustStrength = asset.GustStrength,
                gustFrequency = asset.GustFrequency,
                squallStrength = asset.SquallStrength,
                squallFrequency = asset.SquallFrequency
            };

            return profile;
        }

        private void SeedRandom()
        {
            unchecked
            {
                ulong sceneHandle = gameObject.scene.handle.GetRawData();
                uint sceneHash = (uint)(sceneHandle ^ (sceneHandle >> 32)) + 1u;
                _rngState = sceneHash * 747796405u + 2891336453u;
                if (_rngState == 0u)
                    _rngState = 1u;
            }

            _gustTimeOffset = NextRandom01() * 1000f;
        }

        private float NextRandom01()
        {
            _rngState ^= _rngState << 13;
            _rngState ^= _rngState >> 17;
            _rngState ^= _rngState << 5;
            return (_rngState & 0x00FFFFFFu) / 16777215f;
        }

        private void BuildFallbackProfiles()
        {
            _fallbackProfiles[0] = CreateFallbackProfile(
                SurfaceWeatherKind.ClearCalm,
                1.35f,
                180f,
                320f,
                0.38f,
                0.24f,
                0.55f,
                new Vector2(1f, 0.15f),
                1.08f,
                1f,
                0.95f,
                new Color(0.95f, 0.98f, 1f, 1f),
                new Color(0.58f, 0.62f, 0.72f, 1f),
                new Color(1.18f, 0.63f, 0.31f, 1f),
                new Color(0.08f, 0.09f, 0.12f, 1f),
                new Color(0.77f, 0.82f, 0.88f, 1f),
                0.0008f,
                new Color(0.57f, 0.6f, 0.64f, 1f),
                1.08f,
                1.05f,
                1.05f,
                8f,
                0.75f,
                0.8f,
                1f,
                0f,
                0f,
                0f,
                0.06f,
                0.7f,
                2.2f,
                120f,
                280f,
                0.42f,
                2.2f,
                0.82f,
                0.38f,
                0.76f,
                0.98f,
                0.9f,
                0.7f,
                0.85f,
                0.7f,
                0.8f,
                0.85f,
                0.04f,
                0.018f,
                0.02f,
                0.008f);

            _fallbackProfiles[1] = CreateFallbackProfile(
                SurfaceWeatherKind.ClearBreeze,
                1.15f,
                140f,
                260f,
                0.28f,
                0.24f,
                1f,
                new Vector2(1f, 0.35f),
                1f,
                0.9f,
                1f,
                new Color(0.9f, 0.93f, 0.98f, 1f),
                new Color(0.5f, 0.56f, 0.66f, 1f),
                new Color(1.14f, 0.58f, 0.28f, 1f),
                new Color(0.07f, 0.08f, 0.11f, 1f),
                new Color(0.72f, 0.78f, 0.84f, 1f),
                0.0012f,
                new Color(0.52f, 0.55f, 0.6f, 1f),
                0.96f,
                1f,
                1f,
                18f,
                1f,
                1f,
                1f,
                0f,
                0f,
                0f,
                0.07f,
                0.7f,
                2.4f,
                130f,
                300f,
                0.5f,
                2.4f,
                0.84f,
                0.4f,
                0.74f,
                0.98f,
                1f,
                0.82f,
                0.95f,
                0.82f,
                0.88f,
                0.92f,
                0.08f,
                0.024f,
                0.06f,
                0.01f);

            _fallbackProfiles[2] = CreateFallbackProfile(
                SurfaceWeatherKind.Overcast,
                0.95f,
                120f,
                220f,
                0.16f,
                0.3f,
                1.15f,
                new Vector2(0.8f, 0.6f),
                0.82f,
                0.25f,
                1.2f,
                new Color(0.7f, 0.74f, 0.8f, 1f),
                new Color(0.32f, 0.36f, 0.44f, 1f),
                new Color(0.9f, 0.46f, 0.24f, 1f),
                new Color(0.05f, 0.05f, 0.08f, 1f),
                new Color(0.63f, 0.68f, 0.73f, 1f),
                0.0019f,
                new Color(0.42f, 0.45f, 0.5f, 1f),
                0.72f,
                0.82f,
                0.86f,
                28f,
                1.15f,
                1.12f,
                1.05f,
                0.18f,
                0.08f,
                0.2f,
                0.08f,
                1f,
                2.5f,
                150f,
                340f,
                0.56f,
                2.8f,
                0.88f,
                0.42f,
                0.72f,
                0.96f,
                1.05f,
                0.92f,
                1.05f,
                0.94f,
                0.96f,
                0.96f,
                0.18f,
                0.03f,
                0.18f,
                0.012f);

            _fallbackProfiles[3] = CreateFallbackProfile(
                SurfaceWeatherKind.HeavyRain,
                0.48f,
                90f,
                180f,
                0.1f,
                0.34f,
                1.35f,
                new Vector2(0.7f, 0.7f),
                0.62f,
                0f,
                1.55f,
                new Color(0.56f, 0.61f, 0.68f, 1f),
                new Color(0.21f, 0.24f, 0.31f, 1f),
                new Color(0.72f, 0.38f, 0.2f, 1f),
                new Color(0.04f, 0.04f, 0.06f, 1f),
                new Color(0.52f, 0.57f, 0.62f, 1f),
                0.0035f,
                new Color(0.31f, 0.34f, 0.39f, 1f),
                0.46f,
                0.58f,
                0.66f,
                44f,
                1.45f,
                1.35f,
                1.1f,
                0.78f,
                0.28f,
                0.6f,
                0.1f,
                0.9f,
                2.2f,
                110f,
                250f,
                0.68f,
                3.4f,
                0.94f,
                0.46f,
                0.68f,
                0.92f,
                1.12f,
                1.15f,
                1.18f,
                1.16f,
                1.08f,
                1.06f,
                0.36f,
                0.042f,
                0.42f,
                0.018f);

            _fallbackProfiles[4] = CreateFallbackProfile(
                SurfaceWeatherKind.ElectricalStorm,
                0.24f,
                70f,
                150f,
                0.06f,
                0.38f,
                1.55f,
                new Vector2(0.55f, 0.83f),
                0.48f,
                0f,
                1.85f,
                new Color(0.48f, 0.53f, 0.61f, 1f),
                new Color(0.15f, 0.17f, 0.24f, 1f),
                new Color(0.62f, 0.31f, 0.16f, 1f),
                new Color(0.03f, 0.03f, 0.05f, 1f),
                new Color(0.45f, 0.49f, 0.54f, 1f),
                0.0048f,
                new Color(0.24f, 0.26f, 0.3f, 1f),
                0.32f,
                0.44f,
                0.52f,
                62f,
                1.8f,
                1.65f,
                1.18f,
                0.92f,
                0.88f,
                1.8f,
                0.12f,
                0.4f,
                1.4f,
                90f,
                220f,
                0.82f,
                4.1f,
                1f,
                0.5f,
                0.64f,
                0.88f,
                1.2f,
                1.3f,
                1.28f,
                1.26f,
                1.2f,
                1.18f,
                0.58f,
                0.055f,
                0.58f,
                0.022f);
        }

        private static RuntimeWeatherProfile CreateFallbackProfile(
            SurfaceWeatherKind kind,
            float selectionWeight,
            float minDurationSeconds,
            float maxDurationSeconds,
            float cloudDensityThreshold,
            float cloudSoftness,
            float cloudSpeedMultiplier,
            Vector2 windDirection,
            float skyLuminanceMultiplier,
            float starVisibilityMultiplier,
            float stormEmissionMultiplier,
            Color cloudLitColor,
            Color cloudShadowColor,
            Color sunsetCloudColor,
            Color nightCloudColor,
            Color surfaceFogColor,
            float surfaceFogDensity,
            Color surfaceAmbientColor,
            float surfaceSunMultiplier,
            float sunDiscMultiplier,
            float sunScatterMultiplier,
            float oceanWindSpeedKmh,
            float oceanFoamStrength,
            float oceanFoamCoverage,
            float oceanFoamScale,
            float precipitationIntensity,
            float electricalActivity,
            float lightningFlashIntensity,
            float lightningFlashDuration,
            float thunderDelayMin,
            float thunderDelayMax,
            float lightningStrikeDistanceMin,
            float lightningStrikeDistanceMax,
            float lightningWindBias,
            float thunderPropagationDistanceScale,
            float thunderVolumeNear,
            float thunderVolumeFar,
            float thunderPitchMin,
            float thunderPitchMax,
            float localRainAreaScale,
            float localRainDensityMultiplier,
            float surfaceImpactRadiusScale,
            float surfaceImpactDensityMultiplier,
            float lightningBoltWidthMultiplier,
            float lightningLightRangeMultiplier,
            float gustStrength,
            float gustFrequency,
            float squallStrength,
            float squallFrequency)
        {
            RuntimeWeatherProfile profile;
            profile.kind = kind;
            profile.selectionWeight = selectionWeight;
            profile.minDurationSeconds = minDurationSeconds;
            profile.maxDurationSeconds = maxDurationSeconds;
            profile.state = new WeatherFrameState
            {
                cloudDensityThreshold = cloudDensityThreshold,
                cloudSoftness = cloudSoftness,
                cloudSpeedMultiplier = cloudSpeedMultiplier,
                windDirection = windDirection,
                skyLuminanceMultiplier = skyLuminanceMultiplier,
                starVisibilityMultiplier = starVisibilityMultiplier,
                stormEmissionMultiplier = stormEmissionMultiplier,
                cloudLitColor = cloudLitColor,
                cloudShadowColor = cloudShadowColor,
                sunsetCloudColor = sunsetCloudColor,
                nightCloudColor = nightCloudColor,
                surfaceFogColor = surfaceFogColor,
                surfaceFogDensity = surfaceFogDensity,
                surfaceAmbientColor = surfaceAmbientColor,
                surfaceSunMultiplier = surfaceSunMultiplier,
                sunDiscMultiplier = sunDiscMultiplier,
                sunScatterMultiplier = sunScatterMultiplier,
                oceanWindSpeedKmh = oceanWindSpeedKmh,
                oceanFoamStrength = oceanFoamStrength,
                oceanFoamCoverage = oceanFoamCoverage,
                oceanFoamScale = oceanFoamScale,
                precipitationIntensity = precipitationIntensity,
                electricalActivity = electricalActivity,
                lightningFlashIntensity = lightningFlashIntensity,
                lightningFlashDuration = lightningFlashDuration,
                thunderDelayMin = thunderDelayMin,
                thunderDelayMax = thunderDelayMax,
                lightningStrikeDistanceMin = lightningStrikeDistanceMin,
                lightningStrikeDistanceMax = lightningStrikeDistanceMax,
                lightningWindBias = lightningWindBias,
                thunderPropagationDistanceScale = thunderPropagationDistanceScale,
                thunderVolumeNear = thunderVolumeNear,
                thunderVolumeFar = thunderVolumeFar,
                thunderPitchMin = thunderPitchMin,
                thunderPitchMax = thunderPitchMax,
                localRainAreaScale = localRainAreaScale,
                localRainDensityMultiplier = localRainDensityMultiplier,
                surfaceImpactRadiusScale = surfaceImpactRadiusScale,
                surfaceImpactDensityMultiplier = surfaceImpactDensityMultiplier,
                lightningBoltWidthMultiplier = lightningBoltWidthMultiplier,
                lightningLightRangeMultiplier = lightningLightRangeMultiplier,
                gustStrength = gustStrength,
                gustFrequency = gustFrequency,
                squallStrength = squallStrength,
                squallFrequency = squallFrequency
            };

            return profile;
        }

#if UNITY_EDITOR
        private static readonly string[] EditorDefaultProfilePaths =
        {
            "Assets/_Project/Data/Atmosphere/SurfaceWeather/SurfaceWeatherProfile_ClearCalm.asset",
            "Assets/_Project/Data/Atmosphere/SurfaceWeather/SurfaceWeatherProfile_ClearBreeze.asset",
            "Assets/_Project/Data/Atmosphere/SurfaceWeather/SurfaceWeatherProfile_Overcast.asset",
            "Assets/_Project/Data/Atmosphere/SurfaceWeather/SurfaceWeatherProfile_HeavyRain.asset",
            "Assets/_Project/Data/Atmosphere/SurfaceWeather/SurfaceWeatherProfile_ElectricalStorm.asset"
        };

        private static readonly string[] EditorDefaultThunderClipPaths =
        {
            "Assets/Feel/NiceVibrations/HapticSamples/Nature/Thunder1.wav",
            "Assets/Feel/NiceVibrations/HapticSamples/Nature/Thunder2.wav"
        };

        private void TryAssignEditorAuthoringDefaults()
        {
            if (weatherProfiles == null || weatherProfiles.Length == 0)
            {
                int validCount = 0;
                SurfaceWeatherProfile[] loadedProfiles = new SurfaceWeatherProfile[EditorDefaultProfilePaths.Length];
                for (int i = 0; i < EditorDefaultProfilePaths.Length; i++)
                {
                    SurfaceWeatherProfile profile = AssetDatabase.LoadAssetAtPath<SurfaceWeatherProfile>(EditorDefaultProfilePaths[i]);
                    loadedProfiles[i] = profile;
                    if (profile != null)
                        validCount++;
                }

                if (validCount > 0)
                    weatherProfiles = loadedProfiles;
            }

            if (thunderClips == null || thunderClips.Length == 0)
            {
                int validCount = 0;
                AudioClip[] loadedClips = new AudioClip[EditorDefaultThunderClipPaths.Length];
                for (int i = 0; i < EditorDefaultThunderClipPaths.Length; i++)
                {
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorDefaultThunderClipPaths[i]);
                    loadedClips[i] = clip;
                    if (clip != null)
                        validCount++;
                }

                if (validCount > 0)
                    thunderClips = loadedClips;
            }
        }

        private void TryAssignEditorSceneReferences()
        {
            if (playerMovement == null && SceneBootstrap.CurrentPlayerTransform != null)
                SceneBootstrap.CurrentPlayerTransform.TryGetComponent(out playerMovement);

            if (underwaterVisuals == null)
                TryGetComponent(out underwaterVisuals);

            if (acousticZoneController == null)
                acousticZoneController = GetComponent<AcousticZoneController>();

            if (weatherVfxRig == null)
                weatherVfxRig = GetComponentInChildren<SurfaceWeatherVfxRig>(true);

            if (celestialEngine == null)
                celestialEngine = FindAnyObjectByType<HectonCelestialEngine>();
        }

        private void Reset()
        {
            TryAssignEditorAuthoringDefaults();
            TryAssignEditorSceneReferences();
        }

        private void OnValidate()
        {
            TryAssignEditorAuthoringDefaults();
            TryAssignEditorSceneReferences();

            if (surfaceSuppressionDepth < surfaceActivationDepth)
                surfaceSuppressionDepth = surfaceActivationDepth;

            if (weatherBlendDuration < 1f)
                weatherBlendDuration = 1f;

            if (surfaceSuppressionDelay < 0.1f)
                surfaceSuppressionDelay = 0.1f;
        }
#endif
    }
}
