using Hecton8.Atmosphere;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Visor
{
    /// <summary>
    /// Player-local procedural caustics field generator backed by a compute shader and an R8 render texture.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CausticsProjectorManager : MonoBehaviour, ITickable, ISlowTickable
    {
#if UNITY_EDITOR
        private const string CausticsComputeAssetPath = "Assets/_Project/Art/Shaders/HectonCausticsProjector.compute";
#endif

        private const int FieldResolution = 256;
        private const int ThreadGroupSize = 8;
        private static readonly int _CausticsTextureId = Shader.PropertyToID("_HectonProjectedCausticsTex");
        private static readonly int _CausticsWorldRectId = Shader.PropertyToID("_HectonProjectedCausticsWorldRect");
        private static readonly int _CausticsParamsId = Shader.PropertyToID("_HectonProjectedCausticsParams");
        private static readonly int _CausticsColorId = Shader.PropertyToID("_HectonProjectedCausticsColor");
        private static readonly int _CausticsTexelSizeId = Shader.PropertyToID("_HectonProjectedCausticsTexelSize");
        private static readonly int _CausticsOutputId = Shader.PropertyToID("_HectonCausticsOutput");
        private static readonly int _CausticsSimulationParamsAId = Shader.PropertyToID("_HectonCausticsSimulationParamsA");
        private static readonly int _CausticsSimulationParamsBId = Shader.PropertyToID("_HectonCausticsSimulationParamsB");
        private static readonly int _CausticsSimulationParamsCId = Shader.PropertyToID("_HectonCausticsSimulationParamsC");

        [Header("Compute")]
        [SerializeField]
        [Tooltip("Compute shader that writes the projected caustics intensity field into a persistent R8 render texture.")]
        private ComputeShader causticsCompute;

        [SerializeField, Range(64f, 192f)]
        [Tooltip("World-space width and length covered by the projected caustics field around the player.")]
        private float causticsWorldSize = 96f;

        [SerializeField, Range(0.25f, 2.5f)]
        [Tooltip("Final intensity multiplier for projected floor caustics.")]
        private float causticsIntensity = 0.42f;

        [SerializeField]
        [Tooltip("Additive shallow-water scattering tint applied by Hecton_CoreLit on ocean-floor materials.")]
        private Color scatteringColor = new Color(0.12f, 0.34f, 0.42f, 1f);

        [Header("Depth Gating")]
        [SerializeField, Min(0f)]
        [Tooltip("Depth where projected caustics begin to fade from full intensity.")]
        private float depthFadeStart = 1.5f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Additional depth range over which projected caustics fade out completely.")]
        private float depthFadeRange = 96f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Additional fade applied while storm electrical activity is high near the surface.")]
        private float stormFadePenalty = 0.28f;

        [Header("Voronoi Pattern")]
        [SerializeField, Range(4f, 32f)]
        [Tooltip("Primary Voronoi cell density across the projected caustics field.")]
        private float primaryCellDensity = 12f;

        [SerializeField, Range(8f, 48f)]
        [Tooltip("Secondary Voronoi cell density used to break repetition and tighten highlights.")]
        private float secondaryCellDensity = 22f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Animation speed applied to the primary Voronoi layer.")]
        private float primaryScrollSpeed = 0.32f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Animation speed applied to the secondary Voronoi layer.")]
        private float secondaryScrollSpeed = 0.57f;

        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("Sharpness exponent applied to the Voronoi ridge mask.")]
        private float ridgeSharpness = 3.1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Blend weight of the higher-frequency Voronoi layer.")]
        private float secondaryLayerWeight = 0.42f;

        [Header("Diagnostics")]
        [SerializeField] private float _debugFade01;
        [SerializeField] private float _debugDepthMeters;
        [SerializeField] private Vector4 _debugWorldRect;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _computeReady;
        private int _kernelIndex = -1;
        private float _fade01;
        private HectonSurvivalSystem _survivalSystem;
        private Transform _playerTransform;
        private Camera _gameplayCamera;
        private RenderTexture _causticsTexture;
        private Vector4 _worldRect;

        private void Awake()
        {
            _playerTransform = transform;
            ResolveDependencies();
            EnsureResources();
            PublishGlobals();
        }

        private void OnEnable()
        {
            TryRegisterTickHandlers();
            EnsureResources();
            PublishGlobals();
        }

        private void OnDisable()
        {
            TryUnregisterTickHandlers();
            Shader.SetGlobalVector(_CausticsParamsId, Vector4.zero);
        }

        private void OnDestroy()
        {
            TryUnregisterTickHandlers();
            ReleaseResources();
        }

        /// <summary>
        /// Dispatches the procedural Voronoi caustics compute shader and publishes the world-space projection globals.
        /// </summary>
        /// <param name="deltaTime">Tick delta supplied by the dispatcher.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                return;

            ResolveDependencies();
            EnsureResources();
            UpdateWorldRect();

            if (!_computeReady || _causticsTexture == null || _fade01 <= 0.001f)
            {
                PublishGlobals();
                return;
            }

            float waterLevel = ResolveWaterLevel();
            float timeValue = Time.unscaledTime;
            causticsCompute.SetTexture(_kernelIndex, _CausticsOutputId, _causticsTexture);
            causticsCompute.SetVector(_CausticsSimulationParamsAId, new Vector4(primaryCellDensity, secondaryCellDensity, primaryScrollSpeed, secondaryScrollSpeed));
            causticsCompute.SetVector(_CausticsSimulationParamsBId, new Vector4(ridgeSharpness, secondaryLayerWeight, timeValue, 0f));
            causticsCompute.SetVector(_CausticsSimulationParamsCId, new Vector4(_worldRect.x, _worldRect.y, _worldRect.z, _worldRect.w));
            causticsCompute.SetVector(_CausticsTexelSizeId, new Vector4(1f / FieldResolution, 1f / FieldResolution, FieldResolution, FieldResolution));

            int dispatchCount = (int)math.ceil(FieldResolution / (float)ThreadGroupSize);
            causticsCompute.Dispatch(_kernelIndex, dispatchCount, dispatchCount, 1);

            Shader.SetGlobalTexture(_CausticsTextureId, _causticsTexture);
            Shader.SetGlobalVector(_CausticsWorldRectId, _worldRect);
            Shader.SetGlobalVector(
                _CausticsParamsId,
                new Vector4(
                    _fade01 * math.max(0f, causticsIntensity),
                    waterLevel,
                    depthFadeStart,
                    1f / math.max(0.01f, depthFadeRange)));
            Shader.SetGlobalVector(_CausticsColorId, scatteringColor.linear);
        }

        /// <summary>
        /// Re-resolves depth-based visibility and weather attenuation for the projected caustics field.
        /// </summary>
        public void SlowTick()
        {
            ResolveDependencies();

            float depthMeters = _survivalSystem != null ? math.max(0f, _survivalSystem.Depth) : 0f;
            float fadeIn = math.saturate(depthMeters / math.max(0.01f, depthFadeStart));
            float fadeOut = 1f - math.saturate((depthMeters - depthFadeStart) / math.max(0.01f, depthFadeRange));
            float fade = fadeIn * fadeOut;

            DepthZoneProfile depthZone = DepthZoneDirector.Instance != null ? DepthZoneDirector.Instance.CurrentZone : null;
            if (depthZone != null && depthZone.dangerLevel >= 0.75f)
                fade *= 0.7f;

            HectonSurfaceWeatherDirector weatherDirector = HectonSurfaceWeatherDirector.Instance;
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
        }

        private void EnsureResources()
        {
#if UNITY_EDITOR
            if (causticsCompute == null)
                causticsCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(CausticsComputeAssetPath);
#endif

            if (causticsCompute == null)
            {
                _computeReady = false;
                return;
            }

            if (_kernelIndex < 0)
                _kernelIndex = causticsCompute.FindKernel("ProjectCaustics");

            if (_causticsTexture == null)
            {
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor(FieldResolution, FieldResolution)
                {
                    dimension = TextureDimension.Tex2D,
                    graphicsFormat = GraphicsFormat.R8_UNorm,
                    depthBufferBits = 0,
                    msaaSamples = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    enableRandomWrite = true,
                    sRGB = false
                };
                _causticsTexture = new RenderTexture(descriptor)
                {
                    name = "__HectonProjectedCaustics",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: RenderTexture[1] - persistent R8 projected caustics field - owner: CausticsProjectorManager
                _causticsTexture.Create();
            }

            _computeReady = _kernelIndex >= 0;
        }

        private void ReleaseResources()
        {
            if (_causticsTexture != null)
            {
                _causticsTexture.Release();
                Destroy(_causticsTexture);
                _causticsTexture = null;
            }
        }

        private void UpdateWorldRect()
        {
            Vector3 anchor = _gameplayCamera != null ? _gameplayCamera.transform.position : (_playerTransform != null ? _playerTransform.position : transform.position);
            float worldSize = math.max(16f, causticsWorldSize);
            float halfSize = worldSize * 0.5f;
            _worldRect = new Vector4(
                anchor.x - halfSize,
                anchor.z - halfSize,
                1f / worldSize,
                1f / worldSize);
            _debugWorldRect = _worldRect;
        }

        private void PublishGlobals()
        {
            if (_causticsTexture == null)
            {
                Shader.SetGlobalVector(_CausticsParamsId, Vector4.zero);
                return;
            }

            Shader.SetGlobalTexture(_CausticsTextureId, _causticsTexture);
            Shader.SetGlobalVector(_CausticsWorldRectId, _worldRect);
            Shader.SetGlobalVector(_CausticsColorId, scatteringColor.linear);
            Shader.SetGlobalVector(
                _CausticsParamsId,
                new Vector4(
                    _fade01 * math.max(0f, causticsIntensity),
                    ResolveWaterLevel(),
                    depthFadeStart,
                    1f / math.max(0.01f, depthFadeRange)));
        }

        private float ResolveWaterLevel()
        {
            HectonFluidEngine fluidEngine = HectonFluidEngine.Instance;
            if (fluidEngine != null)
                return fluidEngine.WaterLevel;

            return 4900f;
        }

        private void TryRegisterTickHandlers()
        {
            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
                _registeredSlowTick = true;
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
