using Hecton8.Atmosphere;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Visor
{
    /// <summary>
    /// Player-local shader-only caustics state publisher. No render texture, compute shader, or dispatch is owned here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CausticsProjectorManager : MonoBehaviour, ITickable, ISlowTickable
    {
        private const float DependencyResolveRetryIntervalSeconds = 0.5f;
        private static readonly int _CausticsWorldRectId = Shader.PropertyToID("_HectonProjectedCausticsWorldRect");
        private static readonly int _CausticsParamsId = Shader.PropertyToID("_HectonProjectedCausticsParams");
        private static readonly int _CausticsColorId = Shader.PropertyToID("_HectonProjectedCausticsColor");
        private static readonly int _CausticsSimulationParamsAId = Shader.PropertyToID("_HectonCausticsSimulationParamsA");
        private static readonly int _CausticsSimulationParamsBId = Shader.PropertyToID("_HectonCausticsSimulationParamsB");
        private static readonly int _CausticsSimulationParamsCId = Shader.PropertyToID("_HectonCausticsSimulationParamsC");
        private static readonly int _AbyssalFlowWeatherCurrentId = Shader.PropertyToID("_AbyssalFlowWeatherCurrent");

        [Header("Shader Field")]
        [SerializeField, Range(64f, 192f)]
        private float causticsWorldSize = 96f;
        [SerializeField, Range(0.25f, 2.5f)]
        private float causticsIntensity = 0.42f;
        [SerializeField]
        private Color scatteringColor = new Color(0.12f, 0.34f, 0.42f, 1f);

        [Header("Depth Gating")]
        [SerializeField, Min(0f)]
        private float depthFadeStart = 1.5f;
        [SerializeField, Min(0.1f)]
        private float depthFadeRange = 96f;
        [SerializeField, Range(0f, 1f)]
        private float stormFadePenalty = 0.28f;

        [Header("ALU Pattern")]
        [SerializeField, Range(4f, 32f)]
        private float primaryCellDensity = 12f;
        [SerializeField, Range(8f, 48f)]
        private float secondaryCellDensity = 22f;
        [SerializeField, Range(0f, 2f)]
        private float primaryScrollSpeed = 0.32f;
        [SerializeField, Range(0f, 2f)]
        private float secondaryScrollSpeed = 0.57f;
        [SerializeField, Range(0.1f, 8f)]
        private float ridgeSharpness = 3.1f;
        [SerializeField, Range(0f, 1f)]
        private float secondaryLayerWeight = 0.42f;

        [Header("Diagnostics")]
        [SerializeField] private float _debugFade01;
        [SerializeField] private float _debugDepthMeters;
        [SerializeField] private Vector4 _debugWorldRect;
        [SerializeField] private Vector4 _debugAbyssalFlowWeatherCurrent;
        [SerializeField] private float _debugWaveDisplacement;
        [SerializeField] private Vector2 _debugWaveFlow;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private float _fade01;
        private IHectonOceanKinematics _oceanKinematics;
        private HectonSurvivalSystem _survivalSystem;
        private Transform _playerTransform;
        private Camera _gameplayCamera;
        private Vector4 _worldRect;
        private float _nextDependencyResolveTime;

        private void Awake()
        {
            _playerTransform = transform;
            ResolveDependencies();
            PublishShaderOnlyGlobals(Time.unscaledTime);
        }

        private void OnEnable()
        {
            TryRegisterTickHandlers();
            PublishShaderOnlyGlobals(Time.unscaledTime);
        }

        private void OnDisable()
        {
            TryUnregisterTickHandlers();
            Shader.SetGlobalVector(_CausticsParamsId, Vector4.zero);
        }

        private void OnDestroy()
        {
            TryUnregisterTickHandlers();
            Shader.SetGlobalVector(_CausticsParamsId, Vector4.zero);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                return;

            ResolveDependenciesThrottled();
            PublishShaderOnlyGlobals(Time.unscaledTime);
        }

        public void SlowTick()
        {
            ResolveDependencies();

            float depthMeters = _survivalSystem != null ? math.max(0f, _survivalSystem.Depth) : 0f;
            float fadeIn = math.saturate(depthMeters / math.max(0.01f, depthFadeStart));
            float fadeOut = 1f - math.saturate((depthMeters - depthFadeStart) / math.max(0.01f, depthFadeRange));
            float fade = fadeIn * fadeOut;

            DepthZoneDirector depthZoneDirector = GlobalRegistry.DepthZone;
            DepthZoneProfile depthZone = depthZoneDirector != null ? depthZoneDirector.CurrentZone : null;
            if (depthZone != null && depthZone.dangerLevel >= 0.75f)
                fade *= 0.7f;

            HectonSurfaceWeatherDirector weatherDirector = GlobalRegistry.SurfaceWeather;
            if (weatherDirector != null && depthMeters <= 80f)
                fade *= 1f - (weatherDirector.CurrentElectricalActivity * stormFadePenalty);

            _fade01 = math.saturate(fade);
            _debugDepthMeters = depthMeters;
            _debugFade01 = _fade01;
        }

        private void ResolveDependencies()
        {
            if (_playerTransform == null && SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                _playerTransform = playerTransform;

            if (_survivalSystem == null)
            {
                if (_playerTransform != null)
                    _playerTransform.TryGetComponent(out _survivalSystem);

                if (_survivalSystem == null)
                    TryGetComponent(out _survivalSystem);
            }

            if (_gameplayCamera == null && _playerTransform != null)
            {
                _gameplayCamera = GlobalRegistry.Player != null && GlobalRegistry.Player.PlayerCamera != null
                    ? GlobalRegistry.Player.PlayerCamera
                    : ComponentReferenceUtility.ResolveOwnedComponent<Camera>(_playerTransform);
            }

            if (_oceanKinematics == null || !_oceanKinematics.IsAvailable)
                _oceanKinematics = HectonOceanRegistry.ActiveProvider;
        }

        private void ResolveDependenciesThrottled()
        {
            if (!NeedsDependencyResolve())
                return;

            float now = Time.unscaledTime;
            if (now < _nextDependencyResolveTime)
                return;

            _nextDependencyResolveTime = now + DependencyResolveRetryIntervalSeconds;
            ResolveDependencies();
        }

        private bool NeedsDependencyResolve()
        {
            return _playerTransform == null ||
                   _survivalSystem == null ||
                   _gameplayCamera == null ||
                   _oceanKinematics == null ||
                   !_oceanKinematics.IsAvailable;
        }

        private void PublishShaderOnlyGlobals(float timeValue)
        {
            UpdateWorldRect();
            float waterLevel = ResolveWaterLevel();
            Vector4 waveCoupling = ResolveWaveCoupling(waterLevel);
            Vector4 abyssalFlowWeatherCurrent = ResolveAbyssalFlowWeatherCurrent(timeValue);

            Shader.SetGlobalVector(_CausticsWorldRectId, _worldRect);
            Shader.SetGlobalVector(_CausticsColorId, scatteringColor.linear);
            Shader.SetGlobalVector(_CausticsSimulationParamsAId, new Vector4(primaryCellDensity, secondaryCellDensity, primaryScrollSpeed, secondaryScrollSpeed));
            Shader.SetGlobalVector(_CausticsSimulationParamsBId, new Vector4(ridgeSharpness, secondaryLayerWeight, timeValue, waterLevel));
            Shader.SetGlobalVector(_CausticsSimulationParamsCId, waveCoupling);
            Shader.SetGlobalVector(_AbyssalFlowWeatherCurrentId, abyssalFlowWeatherCurrent);
            Shader.SetGlobalVector(
                _CausticsParamsId,
                new Vector4(
                    _fade01 * math.max(0f, causticsIntensity),
                    waterLevel,
                    depthFadeStart,
                    1f / math.max(0.01f, depthFadeRange)));
        }

        private void UpdateWorldRect()
        {
            Vector3 anchor = _gameplayCamera != null
                ? _gameplayCamera.transform.position
                : (_playerTransform != null ? _playerTransform.position : transform.position);
            float worldSize = math.max(16f, causticsWorldSize);
            float halfSize = worldSize * 0.5f;
            _worldRect = new Vector4(
                anchor.x - halfSize,
                anchor.z - halfSize,
                1f / worldSize,
                1f / worldSize);
            _debugWorldRect = _worldRect;
        }

        private float ResolveWaterLevel()
        {
            HectonFluidEngine fluidEngine = GlobalRegistry.Fluid;
            return fluidEngine != null ? fluidEngine.WaterLevel : 4900f;
        }

        private Vector4 ResolveWaveCoupling(float waterLevel)
        {
            _debugWaveDisplacement = 0f;
            _debugWaveFlow = Vector2.zero;

            if (_playerTransform == null || _oceanKinematics == null || !_oceanKinematics.IsAvailable)
                return Vector4.zero;

            float3 samplePosition = _playerTransform.position;
            samplePosition.y = waterLevel;
            if (!_oceanKinematics.TrySampleWaveKinematics(
                    samplePosition,
                    minSpatialLength: 2f,
                    out _,
                    out _,
                    out float3 surfaceVelocity,
                    out float3 displacement))
            {
                return Vector4.zero;
            }

            _debugWaveDisplacement = displacement.y;
            _debugWaveFlow = new Vector2(surfaceVelocity.x, surfaceVelocity.z);
            float couplingPhase = displacement.y * 0.31f + math.length(surfaceVelocity.xz) * 0.08f;
            return new Vector4(displacement.y, surfaceVelocity.x, surfaceVelocity.z, couplingPhase);
        }

        private Vector4 ResolveAbyssalFlowWeatherCurrent(float timeValue)
        {
            float3 flow = float3.zero;
            HectonFluidEngine fluidEngine = GlobalRegistry.Fluid;
            if (fluidEngine != null)
            {
                Vector3 samplePosition = _playerTransform != null
                    ? _playerTransform.position
                    : transform.position;

                if (!fluidEngine.TrySampleModAbyssalFlow(samplePosition, out flow))
                    flow = float3.zero;
            }

            if (!math.all(math.isfinite(flow)))
                flow = float3.zero;

            Vector4 resolved = new Vector4(flow.x, flow.y, flow.z, timeValue);
            _debugAbyssalFlowWeatherCurrent = resolved;
            return resolved;
        }

        private void TryRegisterTickHandlers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
                _registeredTick = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_registeredSlowTick)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
                _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
            }
        }

        private void TryUnregisterTickHandlers()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
                _registeredSlowTick = false;
            }
        }
    }
}
