using System;
using Stopwatch = System.Diagnostics.Stopwatch;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Modding;
using Hecton8.Physics;
using Hecton8.World;
using NASAPunk.Visor;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Atmosphere
{
    public readonly struct ThunderAcousticShockEvent
    {
        public readonly Vector3 PositionWS;
        public readonly float RadiusMeters;
        public readonly float Intensity01;
        public readonly float LifetimeSeconds;
        public readonly float CameraShake01;
        public readonly float AcousticEnergy;

        public ThunderAcousticShockEvent(
            Vector3 positionWS,
            float radiusMeters,
            float intensity01,
            float lifetimeSeconds,
            float cameraShake01,
            float acousticEnergy)
        {
            PositionWS = positionWS;
            RadiusMeters = radiusMeters;
            Intensity01 = intensity01;
            LifetimeSeconds = lifetimeSeconds;
            CameraShake01 = cameraShake01;
            AcousticEnergy = acousticEnergy;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Atmosphere/Surface Weather Director")]
    [DefaultExecutionOrder(-4500)]
    public sealed class HectonSurfaceWeatherDirector : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, ILateFrameTickable, IOriginShiftListener
    {
        private const float ExponentialBlendCompletion = 0.99f;
        private const float ExponentialBlendRateScale = 4.6051702f;
        private const float ResolveRetryInterval = 2f;
        private const float LightningFlashSeconds = 0.1f;
        private const float SpeedOfSoundMetersPerSecond = 343f;
        private const float ThunderAcousticShockMinRadiusMeters = 72f;
        private const float ThunderAcousticShockMaxRadiusMeters = 240f;
        private const float ThunderAcousticShockLifetimeSeconds = 8f;
        private const float ThunderAcousticShockEnergyScale = 120000f;
        private const float ThunderCameraShakeScale = 0.35f;
        private const float ScreenSpaceRainFrameTimeShedMs = 14f;
        private const int SurfaceWeatherPerformanceWarningCooldownFrames = 30;
        private const uint SurfaceWeatherSolveBudgetWarningHash = 0x53574657u;
        private const uint SurfaceWeatherSolveBudgetContextHash = 0x53574654u;
        private static readonly long SurfaceWeatherSolveBudgetWarningTicks = Math.Max(1L, Stopwatch.Frequency / 5000L);

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
                    cloudDensityThreshold = math.lerp(from.cloudDensityThreshold, to.cloudDensityThreshold, t),
                    cloudSoftness = math.lerp(from.cloudSoftness, to.cloudSoftness, t),
                    cloudSpeedMultiplier = math.lerp(from.cloudSpeedMultiplier, to.cloudSpeedMultiplier, t),
                    windDirection = ToVector2(math.lerp(ToFloat2(from.windDirection), ToFloat2(to.windDirection), t)),
                    skyLuminanceMultiplier = math.lerp(from.skyLuminanceMultiplier, to.skyLuminanceMultiplier, t),
                    starVisibilityMultiplier = math.lerp(from.starVisibilityMultiplier, to.starVisibilityMultiplier, t),
                    stormEmissionMultiplier = math.lerp(from.stormEmissionMultiplier, to.stormEmissionMultiplier, t),
                    cloudLitColor = ToColor(math.lerp(ToFloat4(from.cloudLitColor), ToFloat4(to.cloudLitColor), t)),
                    cloudShadowColor = ToColor(math.lerp(ToFloat4(from.cloudShadowColor), ToFloat4(to.cloudShadowColor), t)),
                    sunsetCloudColor = ToColor(math.lerp(ToFloat4(from.sunsetCloudColor), ToFloat4(to.sunsetCloudColor), t)),
                    nightCloudColor = ToColor(math.lerp(ToFloat4(from.nightCloudColor), ToFloat4(to.nightCloudColor), t)),
                    surfaceFogColor = ToColor(math.lerp(ToFloat4(from.surfaceFogColor), ToFloat4(to.surfaceFogColor), t)),
                    surfaceFogDensity = math.lerp(from.surfaceFogDensity, to.surfaceFogDensity, t),
                    surfaceAmbientColor = ToColor(math.lerp(ToFloat4(from.surfaceAmbientColor), ToFloat4(to.surfaceAmbientColor), t)),
                    surfaceSunMultiplier = math.lerp(from.surfaceSunMultiplier, to.surfaceSunMultiplier, t),
                    sunDiscMultiplier = math.lerp(from.sunDiscMultiplier, to.sunDiscMultiplier, t),
                    sunScatterMultiplier = math.lerp(from.sunScatterMultiplier, to.sunScatterMultiplier, t),
                    oceanWindSpeedKmh = math.lerp(from.oceanWindSpeedKmh, to.oceanWindSpeedKmh, t),
                    oceanFoamStrength = math.lerp(from.oceanFoamStrength, to.oceanFoamStrength, t),
                    oceanFoamCoverage = math.lerp(from.oceanFoamCoverage, to.oceanFoamCoverage, t),
                    oceanFoamScale = math.lerp(from.oceanFoamScale, to.oceanFoamScale, t),
                    precipitationIntensity = math.lerp(from.precipitationIntensity, to.precipitationIntensity, t),
                    electricalActivity = math.lerp(from.electricalActivity, to.electricalActivity, t),
                    lightningFlashIntensity = math.lerp(from.lightningFlashIntensity, to.lightningFlashIntensity, t),
                    lightningFlashDuration = math.lerp(from.lightningFlashDuration, to.lightningFlashDuration, t),
                    thunderDelayMin = math.lerp(from.thunderDelayMin, to.thunderDelayMin, t),
                    thunderDelayMax = math.lerp(from.thunderDelayMax, to.thunderDelayMax, t),
                    lightningStrikeDistanceMin = math.lerp(from.lightningStrikeDistanceMin, to.lightningStrikeDistanceMin, t),
                    lightningStrikeDistanceMax = math.lerp(from.lightningStrikeDistanceMax, to.lightningStrikeDistanceMax, t),
                    lightningWindBias = math.lerp(from.lightningWindBias, to.lightningWindBias, t),
                    thunderPropagationDistanceScale = math.lerp(from.thunderPropagationDistanceScale, to.thunderPropagationDistanceScale, t),
                    thunderVolumeNear = math.lerp(from.thunderVolumeNear, to.thunderVolumeNear, t),
                    thunderVolumeFar = math.lerp(from.thunderVolumeFar, to.thunderVolumeFar, t),
                    thunderPitchMin = math.lerp(from.thunderPitchMin, to.thunderPitchMin, t),
                    thunderPitchMax = math.lerp(from.thunderPitchMax, to.thunderPitchMax, t),
                    localRainAreaScale = math.lerp(from.localRainAreaScale, to.localRainAreaScale, t),
                    localRainDensityMultiplier = math.lerp(from.localRainDensityMultiplier, to.localRainDensityMultiplier, t),
                    surfaceImpactRadiusScale = math.lerp(from.surfaceImpactRadiusScale, to.surfaceImpactRadiusScale, t),
                    surfaceImpactDensityMultiplier = math.lerp(from.surfaceImpactDensityMultiplier, to.surfaceImpactDensityMultiplier, t),
                    lightningBoltWidthMultiplier = math.lerp(from.lightningBoltWidthMultiplier, to.lightningBoltWidthMultiplier, t),
                    lightningLightRangeMultiplier = math.lerp(from.lightningLightRangeMultiplier, to.lightningLightRangeMultiplier, t),
                    gustStrength = math.lerp(from.gustStrength, to.gustStrength, t),
                    gustFrequency = math.lerp(from.gustFrequency, to.gustFrequency, t),
                    squallStrength = math.lerp(from.squallStrength, to.squallStrength, t),
                    squallFrequency = math.lerp(from.squallFrequency, to.squallFrequency, t)
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

        private static readonly int _RainIntensityId = Shader.PropertyToID("_RainIntensity");
        private static readonly int _CurrentWaterLevelYId = Shader.PropertyToID("_CurrentWaterLevelY");
        private static readonly int _GlobalWindId = Shader.PropertyToID("_GlobalWind");
        private static readonly int _ScreenSpaceRainParamsId = Shader.PropertyToID("_HectonScreenSpaceRainParams");
        private static readonly int _ScreenSpaceRainEnabledId = Shader.PropertyToID("_HectonScreenSpaceRainEnabled");
        private static readonly int _LightningFlashId = Shader.PropertyToID("_HectonLightningFlash");

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
        [Tooltip("Seconds used to visually converge local rain exposure after shelter state changes.")]
        [SerializeField, Min(0.05f)] private float shelterExposureBlendTime = 0.45f;

        [Header("References")]
        [Tooltip("Optional explicit player movement reference. If null, runtime resolve is used.")]
        [SerializeField] private HectonPlayerMovement playerMovement;

        [Tooltip("Optional explicit underwater visuals reference. If null, runtime resolve is used.")]
        [SerializeField] private HectonUnderwaterVisuals underwaterVisuals;

        [Tooltip("Optional explicit celestial engine reference. If null, runtime resolve is used.")]
        [SerializeField] private HectonCelestialEngine celestialEngine;

        [Tooltip("Optional explicit ocean-kinematics provider. If null, runtime resolve is used through GlobalRegistry.")]
        [SerializeField] private MonoBehaviour oceanKinematicsProvider;

        [Tooltip("Optional explicit acoustic zone controller reference. If null, runtime resolve is used.")]
        [SerializeField] private AcousticZoneController acousticZoneController;

        [Tooltip("Optional authored local weather VFX rig reference. If null, the director keeps a shader-only weather fallback.")]
        [SerializeField] private SurfaceWeatherVfxRig weatherVfxRig;

        [Tooltip("Shared authored line-renderer material assigned to runtime-created weather VFX rigs.")]
        [SerializeField] private Material lightningBoltMaterial;

        [Tooltip("Optional explicit visor HUD controller reference used for storm interference pulses.")]
        [SerializeField] private VisorHUDController stormVisorController;

        [Tooltip("Optional explicit flashlight reference used for storm interference pulses.")]
        [SerializeField] private PlayerFlashlight stormFlashlight;

        [Header("Storm Equipment Interference")]
        [Tooltip("Electrical activity required before the storm starts glitching the visor and flashlight.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float stormInterferenceElectricalThreshold = 0.42f;
        [Tooltip("Shortest cadence between passive storm interference pulses at peak electrical activity.")]
        [SerializeField, Min(0.05f)] private float stormInterferencePulseIntervalMin = 0.42f;
        [Tooltip("Longest cadence between passive storm interference pulses when the storm only barely exceeds the threshold.")]
        [SerializeField, Min(0.05f)] private float stormInterferencePulseIntervalMax = 1.35f;
        [Tooltip("Visor distortion hold duration for passive storm interference pulses.")]
        [SerializeField, UnityEngine.Range(0f, 0.5f)] private float stormVisorInterferenceHoldDuration = 0.05f;
        [Tooltip("Visor distortion recovery speed for passive storm interference pulses.")]
        [SerializeField, UnityEngine.Range(0.25f, 10f)] private float stormVisorInterferenceRecoverySpeed = 6f;
        [Tooltip("Flashlight interference hold duration for passive storm interference pulses.")]
        [SerializeField, UnityEngine.Range(0f, 0.5f)] private float stormFlashlightInterferenceHoldDuration = 0.08f;
        [Tooltip("Flashlight interference recovery speed for passive storm interference pulses.")]
        [SerializeField, UnityEngine.Range(0.25f, 10f)] private float stormFlashlightInterferenceRecoverySpeed = 6.8f;
        [Tooltip("Short HUD glitch duration for passive storm interference pulses.")]
        [SerializeField, UnityEngine.Range(0f, 0.5f)] private float stormHudGlitchDuration = 0.08f;
        [Tooltip("Longer HUD glitch duration used when a lightning strike fires.")]
        [SerializeField, UnityEngine.Range(0f, 0.5f)] private float lightningHudGlitchDuration = 0.18f;

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
        [SerializeField] private bool _debugScreenSpaceRainShed;

        // COLD ALLOC: RuntimeWeatherProfile[5] - built-in fallback weather library for scenes without authored assets - owner: HectonSurfaceWeatherDirector
        private readonly RuntimeWeatherProfile[] _fallbackProfiles = new RuntimeWeatherProfile[5];
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
        private bool _serviceRegistered;
        private bool _runtimeStateInitialized;
        private bool _bindingsApplied;
        private HectonPlayerMovement _subscribedPlayerMovement;
        private Transform _selfTransform;
        private Transform _playerTransform;
        private BuoyancyObject _playerBuoyancy;

        private IHectonOceanKinematics _oceanKinematics;
        private HectonOceanSurfaceWeatherState _oceanSurfaceDefaults = new HectonOceanSurfaceWeatherState
        {
            FoamStrength = 1f,
            FoamCoverage = 1f,
            FoamScale = 1f
        };
        private HectonOceanSurfaceWeatherState _appliedOceanSurfaceState = new HectonOceanSurfaceWeatherState
        {
            WindSpeed = float.MinValue,
            FoamStrength = float.MinValue,
            FoamCoverage = float.MinValue,
            FoamScale = float.MinValue
        };
        private bool _cachedOceanDefaults;
        private Vector3 _pendingThunderPosition;
        private float _pendingThunderPitch = 1f;
        private float _gustTimeOffset;
        private float _currentLocalRainExposure = 1f;
        private float _targetLocalRainExposure = 1f;
        private bool _isLocallySheltered;
        private float _stormEquipmentPulseTimer;
        private SurfaceWeatherBindingSnapshot _computedBindings;
        private NativeArray<SurfaceWeatherJobOutput> _weatherJobOutput;
        private JobHandle _weatherJobHandle;
        private bool _weatherJobScheduled;
        private bool _weatherJobPrimed;
        private int _nextSurfaceWeatherPerformanceWarningFrame;

        /// <summary>
        /// Active surface weather director instance for the loaded world scene.
        /// </summary>
        public static HectonSurfaceWeatherDirector Instance => GlobalRegistry.SurfaceWeather;

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

        /// <summary>
        /// Returns true when cached interior/depth state blocks local screen-space rain.
        /// </summary>
        public bool IsLocallySheltered => _isLocallySheltered;

        /// <summary>
        /// Current local screen-space rain exposure after shelter blending.
        /// </summary>
        public float CurrentLocalRainExposure => _currentLocalRainExposure;

        private void Awake()
        {
            _selfTransform = transform;
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
            TryRegisterService();
            HectonFloatingOrigin.RegisterListener(this);
            TryRegisterTickManagers();
            TryResolveDependencies(true);
            RefreshPlayerMovementSubscription();
            InitializeRuntimeStateIfNeeded();
            SampleLocalRainExposure();
            _currentLocalRainExposure = _targetLocalRainExposure;
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterTickManagers();

            RefreshPlayerMovementSubscription(null);
            _stormEquipmentPulseTimer = 0f;
            ClearWeatherBindings();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterTickManagers();
            TryUnregisterService();
            RefreshPlayerMovementSubscription(null);
            _stormEquipmentPulseTimer = 0f;
            DisposeWeatherMathBuffers();

        }

        public void Tick(float deltaTime)
        {
            long solveStartTicks = Stopwatch.GetTimestamp();

            TryRegisterTickManagers();
            TryResolveDependencies(false);
            InitializeRuntimeStateIfNeeded();

            if (!_runtimeStateInitialized)
                return;

            UpdateExecutionMode(deltaTime);
            UpdateWeatherSelection(deltaTime);
            ApplyWeatherBindings(deltaTime);
            UpdateDiagnostics();
            ScheduleWeatherMathJob(deltaTime);
            PublishSurfaceWeatherSolveWarningIfNeeded(solveStartTicks);
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

        /// <inheritdoc />
        public void LateFrameTick()
        {
            TryCompleteWeatherMathJob();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            _pendingThunderPosition += -shiftData.ShiftOffset;
        }

        private void TryRegisterTickManagers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredTick = GlobalRegistry.Updatables.Contains(this) ||
                                  SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
            }

            if (!_registeredSlowTick)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
            }
        }

        private void TryUnregisterTickManagers()
        {
            if (_registeredTick)
            {
                if (GlobalRegistry.Updatables.Contains(this))
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

                if (SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this))
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterSurfaceWeatherRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.SurfaceWeather, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterSurfaceWeatherRuntime(this);
            _serviceRegistered = false;
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
            ResolveOceanKinematics();

            if (weatherVfxRig == null)
                ResolveOwnedWeatherVfxRig();

            RefreshPlayerMovementSubscription();
            CacheOceanDefaults();
        }

        private void ResolvePlayerMovementReference()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerMovement != null)
            {
                playerMovement = playerContext.PlayerMovement;
                _playerTransform = playerContext.PlayerTransform;
                _playerBuoyancy = null;
                if (_playerTransform != null)
                    _playerTransform.TryGetComponent(out _playerBuoyancy);
                return;
            }

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform currentPlayerTransform) &&
                currentPlayerTransform != null)
            {
                _playerTransform = currentPlayerTransform;

                if (playerMovement == null || playerMovement.transform != currentPlayerTransform)
                    currentPlayerTransform.TryGetComponent(out playerMovement);

                currentPlayerTransform.TryGetComponent(out _playerBuoyancy);
            }
            else
            {
                _playerTransform = playerMovement != null ? playerMovement.transform : null;
                _playerBuoyancy = null;
                if (_playerTransform != null)
                    _playerTransform.TryGetComponent(out _playerBuoyancy);
            }
        }

        private IHectonOceanKinematics ResolveOceanKinematics()
        {
            if (oceanKinematicsProvider is IHectonOceanKinematics assignedProvider)
            {
                _oceanKinematics = assignedProvider;
                return _oceanKinematics;
            }

            IHectonOceanKinematicsService oceanKinematicsService = GlobalRegistry.OceanKinematics;
            _oceanKinematics = oceanKinematicsService != null
                ? oceanKinematicsService.ActiveProvider
                : null;
            return _oceanKinematics;
        }

        private void ResolveSceneOwnedReferences()
        {
            if (underwaterVisuals == null)
                TryGetComponent(out underwaterVisuals);

            if (acousticZoneController == null)
                acousticZoneController = GlobalRegistry.AcousticZone;

            if (acousticZoneController == null)
                TryGetComponent(out acousticZoneController);

            if (weatherVfxRig == null)
                ResolveOwnedWeatherVfxRig();

            if (_playerTransform != null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (stormVisorController == null || !stormVisorController.transform.IsChildOf(_playerTransform))
                    stormVisorController = playerContext != null ? playerContext.VisorController : null;

                if (stormFlashlight == null || !stormFlashlight.transform.IsChildOf(_playerTransform))
                    stormFlashlight = playerContext != null ? playerContext.Flashlight : null;
            }

            if (celestialEngine != null)
                return;

            celestialEngine = GlobalRegistry.CelestialEngine;
        }

        private void ResolveOwnedWeatherVfxRig()
        {
            Transform rigTransform = transform.Find("SurfaceWeatherVfxRig");
            if (rigTransform != null)
                rigTransform.TryGetComponent(out weatherVfxRig);
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
            _playerBuoyancy = null;
            if (_playerTransform != null)
                _playerTransform.TryGetComponent(out _playerBuoyancy);

            if (_subscribedPlayerMovement != null)
                _subscribedPlayerMovement.OnWaterSplash += HandlePlayerWaterSplash;
        }

        private void CacheOceanDefaults()
        {
            if (_cachedOceanDefaults)
                return;

            IHectonOceanKinematics oceanKinematics = ResolveOceanKinematics();
            if (oceanKinematics == null || !oceanKinematics.TryGetSurfaceWeatherState(out _oceanSurfaceDefaults))
                return;

            _appliedOceanSurfaceState = _oceanSurfaceDefaults;
            _cachedOceanDefaults = true;
        }

        private void InitializeRuntimeStateIfNeeded()
        {
            if (_runtimeStateInitialized)
                return;

            EnsureWeatherMathBuffers();
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
            RunWeatherMathJobCold();
        }

        private void EnsureWeatherMathBuffers()
        {
            if (_weatherJobOutput.IsCreated)
                return;

            _weatherJobOutput = new NativeArray<SurfaceWeatherJobOutput>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SurfaceWeatherJobOutput>[1] - persistent Burst weather output buffer - owner: HectonSurfaceWeatherDirector
            NativeMemorySentinel.RegisterNativeArray(
                _weatherJobOutput,
                nameof(HectonSurfaceWeatherDirector),
                nameof(_weatherJobOutput),
                NativeAllocationLifetime.Scene);
        }

        private void DisposeWeatherMathBuffers()
        {
            if (!_weatherJobOutput.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(_weatherJobOutput);
            if (_weatherJobScheduled)
                _weatherJobOutput.Dispose(_weatherJobHandle);
            else
                _weatherJobOutput.Dispose();

            _weatherJobOutput = default;
            _weatherJobScheduled = false;
            _weatherJobPrimed = false;
        }

        private void RunWeatherMathJobCold()
        {
            if (!_runtimeStateInitialized)
                return;

            EnsureWeatherMathBuffers();
            SurfaceWeatherMathJob job = new SurfaceWeatherMathJob
            {
                input = BuildWeatherJobInput(0f),
                output = _weatherJobOutput
            };

            job.Execute(); // COLD SYNC JOB: direct seed avoids Burst JIT/plugin resolution during Awake.
            CommitWeatherMathOutput(_weatherJobOutput[0]);
            _weatherJobPrimed = true;
        }

        private void TryCompleteWeatherMathJob()
        {
            if (!_weatherJobScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _weatherJobHandle, forceComplete: false))
                return;

            _weatherJobScheduled = false;
            CommitWeatherMathOutput(_weatherJobOutput[0]);
            _weatherJobPrimed = true;
        }

        private void ScheduleWeatherMathJob(float deltaTime)
        {
            if (!_runtimeStateInitialized || _weatherJobScheduled)
                return;

            EnsureWeatherMathBuffers();
            SurfaceWeatherMathJob job = new SurfaceWeatherMathJob
            {
                input = BuildWeatherJobInput(deltaTime),
                output = _weatherJobOutput
            };

            _weatherJobHandle = job.Schedule();
            _weatherJobScheduled = true;
        }

        private SurfaceWeatherJobInput BuildWeatherJobInput(float deltaTime)
        {
            Vector3 followPosition = ResolveFollowPosition();
            Vector3 absoluteOffset = HectonFloatingOrigin.CurrentTotalOffset;
            return new SurfaceWeatherJobInput
            {
                currentState = ToMathState(_currentState),
                targetState = ToMathState(_targetProfile.state),
                deltaTime = math.max(0f, deltaTime),
                weatherBlendDuration = math.max(1f, weatherBlendDuration),
                executionMode = (byte)_executionMode,
                currentLocalRainExposure = _currentLocalRainExposure,
                targetLocalRainExposure = _targetLocalRainExposure,
                shelterExposureBlendTime = math.max(0.05f, shelterExposureBlendTime),
                lightningCooldown = _lightningCooldown,
                lightningFlashRemaining = _lightningFlashRemaining,
                lightningFlashStrength = _lightningFlashStrength,
                pendingThunderDelay = _pendingThunderDelay,
                pendingThunderVolume = _pendingThunderVolume,
                pendingThunderPitch = _pendingThunderPitch,
                pendingThunderPosition = ToFloat3(_pendingThunderPosition),
                gustTimeOffset = _gustTimeOffset,
                unscaledTime = Time.unscaledTime,
                stormEquipmentPulseTimer = _stormEquipmentPulseTimer,
                stormInterferenceElectricalThreshold = stormInterferenceElectricalThreshold,
                stormInterferencePulseIntervalMin = stormInterferencePulseIntervalMin,
                stormInterferencePulseIntervalMax = stormInterferencePulseIntervalMax,
                followPosition = ToFloat3(followPosition),
                absoluteUniverseOffset = new double3(absoluteOffset.x, absoluteOffset.y, absoluteOffset.z),
                surfaceY = ResolveSurfaceY(followPosition),
                randomState = _rngState,
                defaultFoamStrength = _oceanSurfaceDefaults.FoamStrength,
                defaultFoamCoverage = _oceanSurfaceDefaults.FoamCoverage,
                defaultFoamScale = _oceanSurfaceDefaults.FoamScale
            };
        }

        private void CommitWeatherMathOutput(in SurfaceWeatherJobOutput output)
        {
            _currentState = FromMathState(output.currentState);
            _computedBindings = output.bindings;
            _currentLocalRainExposure = output.currentLocalRainExposure;
            _lightningCooldown = output.lightningCooldown;
            _lightningFlashRemaining = output.lightningFlashRemaining;
            _lightningFlashStrength = output.lightningFlashStrength;
            _pendingThunderDelay = output.pendingThunderDelay;
            _pendingThunderVolume = output.pendingThunderVolume;
            _pendingThunderPitch = output.pendingThunderPitch;
            _pendingThunderPosition = ToVector3(output.pendingThunderPosition);
            _stormEquipmentPulseTimer = output.stormEquipmentPulseTimer;
            _rngState = output.randomState;

            if (output.shouldTriggerLightningStormPulse != 0)
            {
                TriggerStormEquipmentPulse(
                    output.stormPulseIntensity,
                    lightningHudGlitchDuration,
                    stormVisorInterferenceHoldDuration * 1.5f,
                    stormVisorInterferenceRecoverySpeed * 0.85f,
                    stormFlashlightInterferenceHoldDuration * 1.4f,
                    stormFlashlightInterferenceRecoverySpeed * 0.8f);
            }
            else if (output.shouldTriggerPassiveStormPulse != 0)
            {
                TriggerStormEquipmentPulse(
                    output.stormPulseIntensity,
                    stormHudGlitchDuration,
                    stormVisorInterferenceHoldDuration,
                    stormVisorInterferenceRecoverySpeed,
                    stormFlashlightInterferenceHoldDuration,
                    stormFlashlightInterferenceRecoverySpeed);
            }

            if (output.shouldTriggerLightning != 0)
            {
                WeatherEvents.RaiseLightning(math.saturate(_lightningFlashStrength));
                if (weatherVfxRig != null &&
                    _executionMode == SurfaceExecutionMode.SurfaceActive)
                {
                    weatherVfxRig.TriggerLightningStrike(
                        ToVector3(output.lightningImpactPosition),
                        _currentState.windDirection,
                        _lightningFlashStrength,
                        output.lightningPhaseA,
                        output.lightningPhaseB,
                        output.lightningBoltWidth,
                        output.lightningLightRange);
                }
            }

            if (output.shouldPlayThunder != 0)
                PlayThunder();
        }

        private static SurfaceWeatherMathState ToMathState(in WeatherFrameState state)
        {
            return new SurfaceWeatherMathState
            {
                cloudDensityThreshold = state.cloudDensityThreshold,
                cloudSoftness = state.cloudSoftness,
                cloudSpeedMultiplier = state.cloudSpeedMultiplier,
                windDirection = ToFloat2(state.windDirection),
                skyLuminanceMultiplier = state.skyLuminanceMultiplier,
                starVisibilityMultiplier = state.starVisibilityMultiplier,
                stormEmissionMultiplier = state.stormEmissionMultiplier,
                cloudLitColor = ToFloat4(state.cloudLitColor),
                cloudShadowColor = ToFloat4(state.cloudShadowColor),
                sunsetCloudColor = ToFloat4(state.sunsetCloudColor),
                nightCloudColor = ToFloat4(state.nightCloudColor),
                surfaceFogColor = ToFloat4(state.surfaceFogColor),
                surfaceFogDensity = state.surfaceFogDensity,
                surfaceAmbientColor = ToFloat4(state.surfaceAmbientColor),
                surfaceSunMultiplier = state.surfaceSunMultiplier,
                sunDiscMultiplier = state.sunDiscMultiplier,
                sunScatterMultiplier = state.sunScatterMultiplier,
                oceanWindSpeedKmh = state.oceanWindSpeedKmh,
                oceanFoamStrength = state.oceanFoamStrength,
                oceanFoamCoverage = state.oceanFoamCoverage,
                oceanFoamScale = state.oceanFoamScale,
                precipitationIntensity = state.precipitationIntensity,
                electricalActivity = state.electricalActivity,
                lightningFlashIntensity = state.lightningFlashIntensity,
                lightningFlashDuration = state.lightningFlashDuration,
                thunderDelayMin = state.thunderDelayMin,
                thunderDelayMax = state.thunderDelayMax,
                lightningStrikeDistanceMin = state.lightningStrikeDistanceMin,
                lightningStrikeDistanceMax = state.lightningStrikeDistanceMax,
                lightningWindBias = state.lightningWindBias,
                thunderPropagationDistanceScale = state.thunderPropagationDistanceScale,
                thunderVolumeNear = state.thunderVolumeNear,
                thunderVolumeFar = state.thunderVolumeFar,
                thunderPitchMin = state.thunderPitchMin,
                thunderPitchMax = state.thunderPitchMax,
                localRainAreaScale = state.localRainAreaScale,
                localRainDensityMultiplier = state.localRainDensityMultiplier,
                surfaceImpactRadiusScale = state.surfaceImpactRadiusScale,
                surfaceImpactDensityMultiplier = state.surfaceImpactDensityMultiplier,
                lightningBoltWidthMultiplier = state.lightningBoltWidthMultiplier,
                lightningLightRangeMultiplier = state.lightningLightRangeMultiplier,
                gustStrength = state.gustStrength,
                gustFrequency = state.gustFrequency,
                squallStrength = state.squallStrength,
                squallFrequency = state.squallFrequency
            };
        }

        private static WeatherFrameState FromMathState(in SurfaceWeatherMathState state)
        {
            return new WeatherFrameState
            {
                cloudDensityThreshold = state.cloudDensityThreshold,
                cloudSoftness = state.cloudSoftness,
                cloudSpeedMultiplier = state.cloudSpeedMultiplier,
                windDirection = ToVector2(state.windDirection),
                skyLuminanceMultiplier = state.skyLuminanceMultiplier,
                starVisibilityMultiplier = state.starVisibilityMultiplier,
                stormEmissionMultiplier = state.stormEmissionMultiplier,
                cloudLitColor = ToColor(state.cloudLitColor),
                cloudShadowColor = ToColor(state.cloudShadowColor),
                sunsetCloudColor = ToColor(state.sunsetCloudColor),
                nightCloudColor = ToColor(state.nightCloudColor),
                surfaceFogColor = ToColor(state.surfaceFogColor),
                surfaceFogDensity = state.surfaceFogDensity,
                surfaceAmbientColor = ToColor(state.surfaceAmbientColor),
                surfaceSunMultiplier = state.surfaceSunMultiplier,
                sunDiscMultiplier = state.sunDiscMultiplier,
                sunScatterMultiplier = state.sunScatterMultiplier,
                oceanWindSpeedKmh = state.oceanWindSpeedKmh,
                oceanFoamStrength = state.oceanFoamStrength,
                oceanFoamCoverage = state.oceanFoamCoverage,
                oceanFoamScale = state.oceanFoamScale,
                precipitationIntensity = state.precipitationIntensity,
                electricalActivity = state.electricalActivity,
                lightningFlashIntensity = state.lightningFlashIntensity,
                lightningFlashDuration = state.lightningFlashDuration,
                thunderDelayMin = state.thunderDelayMin,
                thunderDelayMax = state.thunderDelayMax,
                lightningStrikeDistanceMin = state.lightningStrikeDistanceMin,
                lightningStrikeDistanceMax = state.lightningStrikeDistanceMax,
                lightningWindBias = state.lightningWindBias,
                thunderPropagationDistanceScale = state.thunderPropagationDistanceScale,
                thunderVolumeNear = state.thunderVolumeNear,
                thunderVolumeFar = state.thunderVolumeFar,
                thunderPitchMin = state.thunderPitchMin,
                thunderPitchMax = state.thunderPitchMax,
                localRainAreaScale = state.localRainAreaScale,
                localRainDensityMultiplier = state.localRainDensityMultiplier,
                surfaceImpactRadiusScale = state.surfaceImpactRadiusScale,
                surfaceImpactDensityMultiplier = state.surfaceImpactDensityMultiplier,
                lightningBoltWidthMultiplier = state.lightningBoltWidthMultiplier,
                lightningLightRangeMultiplier = state.lightningLightRangeMultiplier,
                gustStrength = state.gustStrength,
                gustFrequency = state.gustFrequency,
                squallStrength = state.squallStrength,
                squallFrequency = state.squallFrequency
            };
        }

        private static float2 ToFloat2(Vector2 value) => new float2(value.x, value.y);
        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static float4 ToFloat4(Color value) => new float4(value.r, value.g, value.b, value.a);
        private static Vector2 ToVector2(float2 value) => new Vector2(value.x, value.y);
        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
        private static Color ToColor(float4 value) => new Color(value.x, value.y, value.z, value.w);

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

            return math.max(0f, playerMovement.CurrentDepth);
        }

        private void HandlePlayerWaterSplash(float intensity)
        {
            if (weatherVfxRig == null || _executionMode != SurfaceExecutionMode.SurfaceActive)
                return;

            float clampedIntensity = math.saturate(intensity * math.lerp(0.85f, 1.2f, _currentState.precipitationIntensity));
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
            return _playerTransform != null ? _playerTransform.position : (_selfTransform != null ? _selfTransform.position : default);
        }

        private float ResolveSurfaceY(Vector3 followPosition)
        {
            HectonFluidEngine fluidEngine = GlobalRegistry.Fluid;
            if (fluidEngine != null)
                return fluidEngine.CurrentWaterLevelY;

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

            float electricalActivity = math.saturate(_currentState.electricalActivity);
            if (electricalActivity <= 0.2f)
                return;

            _lightningCooldown -= deltaTime;
            if (_lightningCooldown > 0f)
                return;

            TriggerLightning(electricalActivity);
        }

        private void TriggerLightning(float electricalActivity)
        {
            float flashDuration = LightningFlashSeconds;
            float flashBase = math.max(0f, _currentState.lightningFlashIntensity);
            float flashVariance = math.lerp(0.7f, 1f, NextRandom01());
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
            WeatherEvents.RaiseLightning(math.saturate(_lightningFlashStrength));
            TriggerStormEquipmentPulse(
                math.lerp(0.58f, 1f, electricalActivity),
                lightningHudGlitchDuration,
                stormVisorInterferenceHoldDuration * 1.5f,
                stormVisorInterferenceRecoverySpeed * 0.85f,
                stormFlashlightInterferenceHoldDuration * 1.4f,
                stormFlashlightInterferenceRecoverySpeed * 0.8f);

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
                    _currentState.lightningLightRangeMultiplier * math.lerp(1f, gustMultiplier, 0.08f));
            }

            ConfigurePendingThunder(strikePlan.impactPosition, followPosition, electricalActivity);
        }

        private void UpdateStormEquipmentInterference(float deltaTime)
        {
            if (_executionMode == SurfaceExecutionMode.SurfaceSuppressed)
            {
                _stormEquipmentPulseTimer = 0f;
                return;
            }

            float stormInterference = ResolveStormInterference01();
            if (stormInterference <= 0f)
            {
                _stormEquipmentPulseTimer = 0f;
                return;
            }

            _stormEquipmentPulseTimer -= deltaTime;
            if (_stormEquipmentPulseTimer > 0f)
                return;

            TriggerStormEquipmentPulse(
                stormInterference,
                stormHudGlitchDuration,
                stormVisorInterferenceHoldDuration,
                stormVisorInterferenceRecoverySpeed,
                stormFlashlightInterferenceHoldDuration,
                stormFlashlightInterferenceRecoverySpeed);

            _stormEquipmentPulseTimer = math.lerp(
                math.max(0.05f, stormInterferencePulseIntervalMax),
                math.max(0.05f, stormInterferencePulseIntervalMin),
                stormInterference);
        }

        private float ResolveStormInterference01()
        {
            float electricalActivity = math.saturate(_currentState.electricalActivity);
            if (electricalActivity <= stormInterferenceElectricalThreshold)
                return 0f;

            float electricalT = math.saturate((electricalActivity - stormInterferenceElectricalThreshold) / math.max(1f - stormInterferenceElectricalThreshold, 0.0001f));
            float precipitationT = math.lerp(0.7f, 1f, math.saturate(_currentState.precipitationIntensity));
            return electricalT * precipitationT;
        }

        private void TriggerStormEquipmentPulse(
            float normalizedIntensity,
            float glitchDuration,
            float visorHoldDuration,
            float visorRecoverySpeed,
            float flashlightHoldDuration,
            float flashlightRecoverySpeed)
        {
            float clampedIntensity = math.saturate(normalizedIntensity);
            if (clampedIntensity <= 0f)
                return;

            if (stormVisorController != null)
            {
                stormVisorController.GlitchPulse(glitchDuration);
                stormVisorController.TriggerEnvironmentalDistortion(
                    clampedIntensity,
                    visorHoldDuration,
                    visorRecoverySpeed);
            }

            if (stormFlashlight != null)
            {
                stormFlashlight.TriggerExternalInterference(
                    clampedIntensity,
                    flashlightHoldDuration,
                    flashlightRecoverySpeed);
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
                preferredDirection *= math.rsqrt(preferredDirection.sqrMagnitude);

            float randomAngle = randomA * math.PI * 2f;
            Vector2 randomDirection = new Vector2(
                CinematicMath.FastCos(randomAngle),
                CinematicMath.FastSin(randomAngle));
            float clampedWindBias = math.saturate(_currentState.lightningWindBias);
            float angularOffset = ((randomB * 2f) - 1f) * math.lerp(1.4f, 0.35f, clampedWindBias);
            Vector2 windBiasedDirection = RotateDirection(preferredDirection, angularOffset);
            Vector2 resolvedDirection = ToVector2(math.lerp(ToFloat2(randomDirection), ToFloat2(windBiasedDirection), clampedWindBias));
            if (resolvedDirection.sqrMagnitude < 0.0001f)
                resolvedDirection = randomDirection;

            resolvedDirection *= math.rsqrt(resolvedDirection.sqrMagnitude);

            float minDistance = math.max(10f, _currentState.lightningStrikeDistanceMin);
            float maxDistance = math.max(minDistance, _currentState.lightningStrikeDistanceMax);
            float distance = math.lerp(minDistance, maxDistance, randomA);

            LightningStrikePlan plan;
            plan.impactPosition = followPosition + new Vector3(resolvedDirection.x, 0f, resolvedDirection.y) * distance;
            plan.impactPosition.y = surfaceY;
            plan.phaseA = randomA;
            plan.phaseB = randomB;
            return plan;
        }

        private void ConfigurePendingThunder(Vector3 strikePosition, Vector3 listenerPosition, float electricalActivity)
        {
            float thunderDistance = ResolveAupThunderDistanceMeters(strikePosition, listenerPosition);
            float minDistance = math.max(10f, _currentState.lightningStrikeDistanceMin);
            float maxDistance = math.max(minDistance, _currentState.lightningStrikeDistanceMax);
            float distanceT = math.saturate((thunderDistance - minDistance) / math.max(maxDistance - minDistance, 0.0001f));
            float loudness = math.lerp(_currentState.thunderVolumeNear, _currentState.thunderVolumeFar, distanceT);
            float stormBoost = math.lerp(0.65f, 1f, electricalActivity);

            _pendingThunderPosition = strikePosition;
            _pendingThunderDelay = thunderDistance / SpeedOfSoundMetersPerSecond;
            _pendingThunderVolume = loudness * stormBoost;
            _pendingThunderPitch = math.lerp(
                _currentState.thunderPitchMin,
                _currentState.thunderPitchMax,
                NextRandom01()) * math.lerp(0.94f, 1.02f, 1f - distanceT);
        }

        private static float ResolveAupThunderDistanceMeters(Vector3 strikePosition, Vector3 listenerPosition)
        {
            AbsoluteUniversePosition strikeAup = AbsoluteUniversePosition.FromRuntimePosition(strikePosition);
            AbsoluteUniversePosition listenerAup = AbsoluteUniversePosition.FromRuntimePosition(listenerPosition);
            return ApproximateDistanceMeters(strikeAup.ToAbsoluteDouble3() - listenerAup.ToAbsoluteDouble3());
        }

        private static Vector2 RotateDirection(Vector2 direction, float angleRadians)
        {
            float sin = CinematicMath.FastSin(angleRadians);
            float cos = CinematicMath.FastCos(angleRadians);
            return new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos);
        }

        private static float ApproximateDistanceMeters(double3 delta)
        {
            double3 absolute = math.abs(delta);
            double maxAxis = math.max(absolute.x, math.max(absolute.y, absolute.z));
            if (!math.isfinite(maxAxis) || maxAxis <= 0d)
                return 0f;

            double minAxis = math.min(absolute.x, math.min(absolute.y, absolute.z));
            double midAxis = absolute.x + absolute.y + absolute.z - maxAxis - minAxis;
            double approximateDistance = maxAxis + (midAxis * 0.375d) + (minAxis * 0.25d);
            return (float)math.min(approximateDistance, (double)float.MaxValue);
        }

        private float ResolveNextLightningCooldown(float electricalActivity)
        {
            float clampedElectrical = math.saturate(electricalActivity);
            float baseInterval = math.lerp(18f, 4.5f, clampedElectrical);
            float jitter = math.lerp(8f, 1.5f, clampedElectrical);
            return math.max(0.5f, baseInterval + ((NextRandom01() * 2f) - 1f) * jitter);
        }

        private void BlendWeather(float deltaTime)
        {
            float blendT = ResolveExponentialBlendFactor(deltaTime, weatherBlendDuration);
            _currentState = WeatherFrameState.Lerp(_currentState, _targetProfile.state, blendT);
        }

        private static float ResolveExponentialBlendFactor(float deltaTime, float durationSeconds)
        {
            float clampedDeltaTime = math.max(0f, deltaTime);
            float duration = math.max(0.0001f, durationSeconds);
            return ApproximateOneMinusExpNegPositive((ExponentialBlendRateScale / duration) * clampedDeltaTime);
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

        private void UpdateDiagnostics()
        {
            _debugExecutionMode = _executionMode;
            _debugHoldTimer = _weatherHoldTimer;
            _debugSuppressionTimer = _surfaceSuppressionTimer;
            _debugLightningFlash = _lightningFlashStrength;
            _debugPrecipitation = _currentState.precipitationIntensity;
            _debugElectricalActivity = _currentState.electricalActivity;
            _debugGustMultiplier = _computedBindings.gustMultiplier;
            _debugSquallMultiplier = _computedBindings.squallMultiplier;
            _debugLocalRainExposure = _currentLocalRainExposure;
            _debugIsSheltered = _isLocallySheltered;
        }

        private void UpdateLocalRainExposure(float deltaTime)
        {
            float blendTime = math.max(0.05f, shelterExposureBlendTime);
            float blendT = math.saturate(deltaTime / blendTime);
            _currentLocalRainExposure = math.lerp(_currentLocalRainExposure, _targetLocalRainExposure, blendT);
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

            bool dryZoneShelter = _playerBuoyancy != null && _playerBuoyancy.IsInDryZone;
            _targetLocalRainExposure = dryZoneShelter ? 0f : 1f;
            _isLocallySheltered = dryZoneShelter;
        }

        private float ResolveGustMultiplier(in WeatherFrameState state)
        {
            float gustStrength = math.saturate(state.gustStrength);
            if (gustStrength <= 0.001f)
                return 1f;

            float frequency = math.clamp(state.gustFrequency, 0.005f, 0.2f);
            float phase = (Time.unscaledTime + _gustTimeOffset) * frequency * math.PI * 2f;
            float composite =
                CinematicMath.FastSin(phase) * 0.58f +
                CinematicMath.FastSin(phase * 0.43f + 1.17f) * 0.29f +
                CinematicMath.FastSin(phase * 1.73f + 0.41f) * 0.13f;

            float normalized = math.saturate((composite + 1f) * 0.5f);
            float envelope = normalized * normalized;
            float calmFloor = 1f - gustStrength * 0.12f;
            float gustPeak = 1f + gustStrength * 0.42f;
            return math.lerp(calmFloor, gustPeak, envelope);
        }

        private float ResolveSquallMultiplier(in WeatherFrameState state)
        {
            float squallStrength = math.saturate(state.squallStrength);
            if (squallStrength <= 0.001f)
                return 1f;

            float frequency = math.clamp(state.squallFrequency, 0.005f, 0.08f);
            float phase = (Time.unscaledTime + _gustTimeOffset * 0.37f) * frequency * math.PI * 2f;
            float composite =
                CinematicMath.FastSin(phase) * 0.61f +
                CinematicMath.FastSin(phase * 0.31f + 2.14f) * 0.27f +
                CinematicMath.FastSin(phase * 1.09f + 0.63f) * 0.12f;

            float normalized = math.saturate((composite + 1f) * 0.5f);
            float bandEnvelope = normalized * normalized * normalized;
            float calmFloor = 1f - squallStrength * 0.26f;
            float squallPeak = 1f + squallStrength * 0.72f;
            return math.lerp(calmFloor, squallPeak, bandEnvelope);
        }

        private void ApplyWeatherBindings(float deltaTime)
        {
            if (!_weatherJobPrimed)
                return;

            if (_executionMode == SurfaceExecutionMode.SurfaceSuppressed)
            {
                ClearWeatherBindings();
                return;
            }

            bool surfaceVfxActive = _executionMode == SurfaceExecutionMode.SurfaceActive;
            SurfaceWeatherBindingSnapshot bindings = _computedBindings;
            Vector3 followPosition = ResolveFollowPosition();
            float surfaceY = ResolveSurfaceY(followPosition);

            if (underwaterVisuals != null)
            {
                underwaterVisuals.SetSurfaceWeatherOverride(
                    _currentState.surfaceFogColor,
                    _currentState.surfaceFogDensity,
                    _currentState.surfaceAmbientColor,
                    bindings.sunMultiplier);
            }

            if (celestialEngine != null)
            {
                celestialEngine.SetSurfaceWeatherOverride(
                    _currentState.cloudDensityThreshold,
                    _currentState.cloudSoftness,
                    bindings.cloudSpeedMultiplier,
                    _currentState.windDirection,
                    _currentState.surfaceFogColor,
                    _currentState.surfaceFogDensity,
                    _currentState.surfaceAmbientColor,
                    bindings.sunMultiplier,
                    _currentState.starVisibilityMultiplier,
                    _currentState.stormEmissionMultiplier,
                    bindings.skyLuminance,
                    bindings.sunDisc,
                    bindings.sunScatter,
                    _currentState.cloudLitColor,
                    _currentState.cloudShadowColor,
                    _currentState.sunsetCloudColor,
                    _currentState.nightCloudColor);
            }

            if (acousticZoneController != null)
            {
                acousticZoneController.SetSurfaceWeatherMix(
                    bindings.acousticPrecipitation,
                    _currentState.electricalActivity);
            }

            if (weatherVfxRig != null)
            {
                weatherVfxRig.ApplyState(
                    deltaTime,
                    followPosition,
                    surfaceY,
                    _currentState.windDirection,
                    bindings.vfxPrecipitation,
                    bindings.localRainAreaScale,
                    bindings.localRainDensityMultiplier,
                    bindings.surfaceImpactRadiusScale,
                    bindings.surfaceImpactDensityMultiplier,
                    surfaceVfxActive);
            }

            PublishWeatherShaderGlobals(surfaceY, bindings, surfaceVfxActive);
            _debugGustMultiplier = bindings.gustMultiplier;
            _debugSquallMultiplier = bindings.squallMultiplier;
            ApplyOceanState(bindings);
            _bindingsApplied = true;
        }

        private void ClearWeatherBindings()
        {
            PublishClearedWeatherShaderGlobals();

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

        private void PublishWeatherShaderGlobals(float surfaceY, in SurfaceWeatherBindingSnapshot bindings, bool surfaceVfxActive)
        {
            bool shedScreenSpaceRain = Time.unscaledDeltaTime * 1000f > ScreenSpaceRainFrameTimeShedMs;
            float rainIntensity = surfaceVfxActive && !shedScreenSpaceRain ? math.saturate(bindings.vfxPrecipitation) : 0f;
            float windSpeedMps = math.max(0f, bindings.targetWindSpeed / 3.6f);
            Vector2 windDirection = _currentState.windDirection;
            float windMagnitudeSq = windDirection.sqrMagnitude;
            if (windMagnitudeSq > 0.0001f)
                windDirection *= math.rsqrt(windMagnitudeSq);
            else
                windDirection = Vector2.zero;

            Shader.SetGlobalFloat(_RainIntensityId, rainIntensity);
            Shader.SetGlobalFloat(_CurrentWaterLevelYId, surfaceY);
            Shader.SetGlobalVector(_GlobalWindId, new Vector4(windDirection.x * windSpeedMps, 0f, windDirection.y * windSpeedMps, windSpeedMps));
            Shader.SetGlobalFloat(_ScreenSpaceRainEnabledId, rainIntensity > 0.0001f ? 1f : 0f);
            Shader.SetGlobalVector(
                _ScreenSpaceRainParamsId,
                new Vector4(
                    rainIntensity,
                    math.max(0f, bindings.localRainDensityMultiplier),
                    math.max(0.1f, bindings.localRainAreaScale),
                    math.saturate(bindings.localRainExposure)));
            if (GlobalRegistry.CelestialEngine == null)
                Shader.SetGlobalFloat(_LightningFlashId, math.saturate(_lightningFlashStrength));
            _debugScreenSpaceRainShed = shedScreenSpaceRain && surfaceVfxActive;
        }

        private static void PublishClearedWeatherShaderGlobals()
        {
            Shader.SetGlobalFloat(_RainIntensityId, 0f);
            if (GlobalRegistry.CelestialEngine == null)
                Shader.SetGlobalFloat(_LightningFlashId, 0f);
            Shader.SetGlobalFloat(_ScreenSpaceRainEnabledId, 0f);
            Shader.SetGlobalVector(_ScreenSpaceRainParamsId, Vector4.zero);
        }

        private void PublishSurfaceWeatherSolveWarningIfNeeded(long solveStartTicks)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - solveStartTicks;
            if (elapsedTicks <= SurfaceWeatherSolveBudgetWarningTicks ||
                Time.frameCount < _nextSurfaceWeatherPerformanceWarningFrame)
            {
                return;
            }

            double elapsedMilliseconds = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            GlobalTelemetryBus.PublishPerformanceWarning(
                SurfaceWeatherSolveBudgetWarningHash,
                SurfaceWeatherSolveBudgetContextHash,
                (float)elapsedMilliseconds);
            _nextSurfaceWeatherPerformanceWarningFrame = Time.frameCount + SurfaceWeatherPerformanceWarningCooldownFrames;
        }

        private void ApplyOceanState(in SurfaceWeatherBindingSnapshot bindings)
        {
            const float OceanPropertyEpsilon = 0.01f;

            CacheOceanDefaults();
            if (!_cachedOceanDefaults)
                return;

            IHectonOceanKinematics oceanKinematics = ResolveOceanKinematics();
            if (oceanKinematics == null)
                return;

            HectonOceanSurfaceWeatherState targetState = _oceanSurfaceDefaults;
            targetState.WindSpeed = bindings.targetWindSpeed;
            targetState.FoamStrength = bindings.targetFoamStrength;
            targetState.FoamCoverage = bindings.targetFoamCoverage;
            targetState.FoamScale = bindings.targetFoamScale;

            if (!NeedsOceanSurfaceStateUpdate(in targetState, OceanPropertyEpsilon))
                return;

            if (oceanKinematics.ApplySurfaceWeatherState(in targetState))
                _appliedOceanSurfaceState = targetState;
        }

        private void RestoreOceanDefaults()
        {
            if (!_cachedOceanDefaults)
                return;

            IHectonOceanKinematics oceanKinematics = ResolveOceanKinematics();
            if (oceanKinematics == null)
                return;

            if (oceanKinematics.ApplySurfaceWeatherState(in _oceanSurfaceDefaults))
                _appliedOceanSurfaceState = _oceanSurfaceDefaults;
        }

        private bool NeedsOceanSurfaceStateUpdate(in HectonOceanSurfaceWeatherState targetState, float epsilon)
        {
            uint flags = targetState.Flags;
            if ((flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsWindSpeed) != 0u &&
                math.abs(_appliedOceanSurfaceState.WindSpeed - targetState.WindSpeed) > epsilon)
            {
                return true;
            }

            if ((flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamStrength) != 0u &&
                math.abs(_appliedOceanSurfaceState.FoamStrength - targetState.FoamStrength) > epsilon)
            {
                return true;
            }

            if ((flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamCoverage) != 0u &&
                math.abs(_appliedOceanSurfaceState.FoamCoverage - targetState.FoamCoverage) > epsilon)
            {
                return true;
            }

            if ((flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamScale) != 0u &&
                math.abs(_appliedOceanSurfaceState.FoamScale - targetState.FoamScale) > epsilon)
            {
                return true;
            }

            return false;
        }

        private void PlayThunder()
        {
            DispatchThunderAcousticShock(_pendingThunderPosition, _pendingThunderVolume);

            if (thunderClips == null || thunderClips.Length == 0)
                return;

            Hecton8.Core.IAudioService audioManager = Hecton8.Core.GlobalRegistry.Audio;
            if (audioManager == null)
                return;

            int clipIndex = math.clamp((int)(NextRandom01() * thunderClips.Length), 0, thunderClips.Length - 1);
            AudioClip clip = thunderClips[clipIndex];
            if (clip == null)
                return;

            if (audioManager is SpatialAudioManager spatialAudioManager)
            {
                spatialAudioManager.PlayWeatherAtPoint(
                    clip,
                    _pendingThunderPosition,
                    _pendingThunderVolume,
                    _pendingThunderPitch,
                    audioManager.AmbientGroup);
                return;
            }

            audioManager.PlayAtPoint(
                clip,
                _pendingThunderPosition,
                _pendingThunderVolume,
                _pendingThunderPitch,
                audioManager.AmbientGroup);
        }

        private static void DispatchThunderAcousticShock(Vector3 shockPosition, float thunderVolume01)
        {
            float intensity01 = math.saturate(thunderVolume01);
            if (intensity01 <= 0f)
                return;

            float radiusMeters = math.lerp(ThunderAcousticShockMinRadiusMeters, ThunderAcousticShockMaxRadiusMeters, intensity01);
            float acousticEnergy = intensity01 * ThunderAcousticShockEnergyScale;
            float cameraShake01 = math.saturate(intensity01 * ThunderCameraShakeScale);
            ThunderAcousticShockEvent shockEvent = new ThunderAcousticShockEvent(
                shockPosition,
                radiusMeters,
                intensity01,
                ThunderAcousticShockLifetimeSeconds,
                cameraShake01,
                acousticEnergy);
            HectonEventBus.Publish(in shockEvent);

            PhysicsEventBus.NotifyAcousticPing(new AcousticPingEvent(
                shockPosition,
                radiusMeters,
                intensity01,
                ThunderAcousticShockLifetimeSeconds,
                FieldTargetRole.HazardProbe,
                0,
                acousticEnergy));

            var cameraJuice = GlobalRegistry.CameraJuice;
            if (cameraJuice != null)
                cameraJuice.TriggerSubmarineImpactShake(cameraShake01);
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

                totalWeight += math.max(0.001f, candidate.selectionWeight);
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

                accumulator += math.max(0.001f, candidate.selectionWeight);
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

            return math.lerp(profile.minDurationSeconds, profile.maxDurationSeconds, NextRandom01());
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

        private const string EditorDefaultLightningBoltMaterialPath = "Assets/_Project/Art/Materials/VFX/Mat_LeakPlume.mat";

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

            if (lightningBoltMaterial == null)
                lightningBoltMaterial = AssetDatabase.LoadAssetAtPath<Material>(EditorDefaultLightningBoltMaterialPath);
        }

        private void TryAssignEditorSceneReferences()
        {
            if (playerMovement == null && GameBootstrapper.CurrentPlayerTransform != null)
                GameBootstrapper.CurrentPlayerTransform.TryGetComponent(out playerMovement);

            if (underwaterVisuals == null)
                TryGetComponent(out underwaterVisuals);

            if (acousticZoneController == null)
                acousticZoneController = GetComponent<AcousticZoneController>();

            if (weatherVfxRig == null)
                ResolveOwnedWeatherVfxRig();

            if (celestialEngine == null)
                celestialEngine = GlobalRegistry.CelestialEngine;
        }

        private void Reset()
        {
            if (EditorApplication.isCompiling || !Application.isPlaying)
                return;

            TryAssignEditorAuthoringDefaults();
            TryAssignEditorSceneReferences();
        }

        private void OnValidate()
        {
            if (EditorApplication.isCompiling || !Application.isPlaying)
                return;

            TryAssignEditorAuthoringDefaults();
            TryAssignEditorSceneReferences();

            if (surfaceSuppressionDepth < surfaceActivationDepth)
                surfaceSuppressionDepth = surfaceActivationDepth;

            if (weatherBlendDuration < 1f)
                weatherBlendDuration = 1f;

            if (surfaceSuppressionDelay < 0.1f)
                surfaceSuppressionDelay = 0.1f;

            if (shelterExposureBlendTime < 0.05f)
                shelterExposureBlendTime = 0.05f;

        }
#endif
    }
}


