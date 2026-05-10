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
    public sealed class CausticsProjectorManager : MonoBehaviour, ISlowTickable
    {
        private const float InvTwoPi = 1f / (math.PI * 2f);
        private const float ShaderVectorPublishEpsilon = 0.0001f;
        private static readonly int _CausticsWorldRectId = Shader.PropertyToID("_HectonProjectedCausticsWorldRect");
        private static readonly int _CausticsParamsId = Shader.PropertyToID("_HectonProjectedCausticsParams");
        private static readonly int _CausticsColorId = Shader.PropertyToID("_HectonProjectedCausticsColor");
        private static readonly int _CausticsSimulationParamsAId = Shader.PropertyToID("_HectonCausticsSimulationParamsA");
        private static readonly int _CausticsSimulationParamsBId = Shader.PropertyToID("_HectonCausticsSimulationParamsB");
        private static readonly int _CausticsSimulationParamsCId = Shader.PropertyToID("_HectonCausticsSimulationParamsC");
        private static readonly int _CausticsTextureParamsId = Shader.PropertyToID("_HectonCausticsTextureParams");
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

        [Header("Analytical Caustics")]
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

        private bool _registeredSlowTick;
        private float _fade01;
        private HectonSurvivalSystem _survivalSystem;
        private Transform _playerTransform;
        private Camera _gameplayCamera;
        private Vector4 _worldRect;
        private Vector4 _publishedCausticsWorldRect;
        private Vector4 _publishedCausticsColor;
        private Vector4 _publishedCausticsSimulationParamsA;
        private Vector4 _publishedCausticsSimulationParamsB;
        private Vector4 _publishedCausticsSimulationParamsC;
        private Vector4 _publishedCausticsTextureParams;
        private Vector4 _publishedAbyssalFlowWeatherCurrent;
        private Vector4 _publishedCausticsParams;
        private bool _hasPublishedCausticsVectors;

        private void Awake()
        {
            _playerTransform = transform;
            ResolveDependencies();
            PublishShaderOnlyGlobals();
        }

        private void OnEnable()
        {
            TryRegisterTickHandlers();
            PublishShaderOnlyGlobals();
        }

        private void OnDisable()
        {
            TryUnregisterTickHandlers();
            Shader.SetGlobalVector(_CausticsParamsId, Vector4.zero);
            Shader.SetGlobalVector(_CausticsTextureParamsId, Vector4.zero);
            InvalidatePublishedShaderVectorCache();
        }

        private void OnDestroy()
        {
            TryUnregisterTickHandlers();
            Shader.SetGlobalVector(_CausticsParamsId, Vector4.zero);
            Shader.SetGlobalVector(_CausticsTextureParamsId, Vector4.zero);
            InvalidatePublishedShaderVectorCache();
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
            PublishShaderOnlyGlobals();
        }

        private void ResolveDependencies()
        {
            if (_playerTransform == null && GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
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

        }

        private void PublishShaderOnlyGlobals()
        {
            Vector3 runtimeAnchor = ResolveRuntimeAnchor();
            UpdateWorldRect(runtimeAnchor);
            float waterLevel = ResolveWaterLevel();
            Vector4 abyssalFlowWeatherCurrent = ResolveAbyssalFlowWeatherCurrent(runtimeAnchor);
            Vector4 waveCoupling = ResolveFakeWaveCoupling(in abyssalFlowWeatherCurrent);

            Color linearScatteringColor = scatteringColor.linear;
            Vector4 causticsColor = new Vector4(
                linearScatteringColor.r,
                linearScatteringColor.g,
                linearScatteringColor.b,
                linearScatteringColor.a);
            Vector4 simulationParamsA = new Vector4(primaryCellDensity, secondaryCellDensity, primaryScrollSpeed, secondaryScrollSpeed);
            Vector4 simulationParamsB = new Vector4(ridgeSharpness, secondaryLayerWeight, 0f, waterLevel);
            Vector4 textureParams = Vector4.zero;
            Vector4 causticsParams = new Vector4(
                _fade01 * math.max(0f, causticsIntensity),
                waterLevel,
                depthFadeStart,
                1f / math.max(0.01f, depthFadeRange));

            SetGlobalVectorIfChanged(_CausticsWorldRectId, _worldRect, ref _publishedCausticsWorldRect);
            SetGlobalVectorIfChanged(_CausticsColorId, causticsColor, ref _publishedCausticsColor);
            SetGlobalVectorIfChanged(_CausticsSimulationParamsAId, simulationParamsA, ref _publishedCausticsSimulationParamsA);
            SetGlobalVectorIfChanged(_CausticsSimulationParamsBId, simulationParamsB, ref _publishedCausticsSimulationParamsB);
            SetGlobalVectorIfChanged(_CausticsSimulationParamsCId, waveCoupling, ref _publishedCausticsSimulationParamsC);
            SetGlobalVectorIfChanged(_CausticsTextureParamsId, textureParams, ref _publishedCausticsTextureParams);
            SetGlobalVectorIfChanged(_AbyssalFlowWeatherCurrentId, abyssalFlowWeatherCurrent, ref _publishedAbyssalFlowWeatherCurrent);
            SetGlobalVectorIfChanged(
                _CausticsParamsId,
                causticsParams,
                ref _publishedCausticsParams);
            _hasPublishedCausticsVectors = true;
        }

        private void UpdateWorldRect(in Vector3 anchor)
        {
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
            Hecton8.Physics.HectonFluidEngine fluidEngine = GlobalRegistry.Fluid;
            return fluidEngine != null ? fluidEngine.WaterLevel : 4900f;
        }

        private Vector4 ResolveFakeWaveCoupling(in Vector4 abyssalFlowWeatherCurrent)
        {
            float2 flowXZ = new float2(abyssalFlowWeatherCurrent.x, abyssalFlowWeatherCurrent.z);
            if (!math.all(math.isfinite(flowXZ)))
                flowXZ = float2.zero;

            float flowEnergy01 = math.saturate(math.lengthsq(flowXZ) * (1f / 400f));
            float phase = math.dot(flowXZ, new float2(0.071f, -0.053f)) + flowEnergy01 * 1.73f;
            float fakeDisplacement =
                (EvaluateCheapWaveSigned(phase) * 0.12f) +
                (EvaluateCheapWaveSigned(phase * 1.6180339f + 1.73f) * 0.045f);
            fakeDisplacement *= math.saturate(0.35f + flowEnergy01 * 0.65f);

            _debugWaveDisplacement = fakeDisplacement;
            _debugWaveFlow = new Vector2(flowXZ.x, flowXZ.y);
            return new Vector4(fakeDisplacement, flowXZ.x, flowXZ.y, phase);
        }

        private Vector3 ResolveRuntimeAnchor()
        {
            if (TryResolvePlayerAupRuntimePosition(out Vector3 aupRuntimePosition))
                return aupRuntimePosition;

            if (_gameplayCamera != null)
                return _gameplayCamera.transform.position;

            if (_playerTransform != null)
                return _playerTransform.position;

            return transform.position;
        }

        private static bool TryResolvePlayerAupRuntimePosition(out Vector3 runtimePosition)
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    float3 runtime = movementState.PredictedAup.ToRuntimeFloat3();
                    runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
                    return true;
                }
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement != null)
            {
                float3 runtime = playerMovement.CurrentAup.ToRuntimeFloat3();
                runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
                return true;
            }

            runtimePosition = default;
            return false;
        }

        private static float EvaluateCheapWaveSigned(float phaseRadians)
        {
            float phase01 = math.frac((phaseRadians * InvTwoPi) + 0.25f);
            float triangle = 1f - math.abs(phase01 * 2f - 1f);
            return (triangle * 2f) - 1f;
        }

        private Vector4 ResolveAbyssalFlowWeatherCurrent(in Vector3 samplePosition)
        {
            float3 flow = float3.zero;
            Hecton8.Physics.HectonFluidEngine fluidEngine = GlobalRegistry.Fluid;
            if (fluidEngine != null)
            {
                if (!fluidEngine.TrySampleModAbyssalFlow(samplePosition, out flow))
                    flow = float3.zero;
            }

            if (!math.all(math.isfinite(flow)))
                flow = float3.zero;

            Vector4 resolved = new Vector4(flow.x, flow.y, flow.z, 0f);
            _debugAbyssalFlowWeatherCurrent = resolved;
            return resolved;
        }

        private void SetGlobalVectorIfChanged(int propertyId, Vector4 value, ref Vector4 cachedValue)
        {
            if (_hasPublishedCausticsVectors && !Vector4Changed(cachedValue, value))
                return;

            Shader.SetGlobalVector(propertyId, value);
            cachedValue = value;
        }

        private static bool Vector4Changed(Vector4 current, Vector4 next)
        {
            return math.abs(current.x - next.x) > ShaderVectorPublishEpsilon ||
                   math.abs(current.y - next.y) > ShaderVectorPublishEpsilon ||
                   math.abs(current.z - next.z) > ShaderVectorPublishEpsilon ||
                   math.abs(current.w - next.w) > ShaderVectorPublishEpsilon;
        }

        private void InvalidatePublishedShaderVectorCache()
        {
            _hasPublishedCausticsVectors = false;
            _publishedCausticsWorldRect = default;
            _publishedCausticsColor = default;
            _publishedCausticsSimulationParamsA = default;
            _publishedCausticsSimulationParamsB = default;
            _publishedCausticsSimulationParamsC = default;
            _publishedCausticsTextureParams = default;
            _publishedAbyssalFlowWeatherCurrent = default;
            _publishedCausticsParams = default;
        }

        private void TryRegisterTickHandlers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterTickHandlers()
        {
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
                _registeredSlowTick = false;
            }
        }
    }
}
