using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Main Menu Atmosphere Controller")]
    public sealed class MainMenuAtmosphereController : MonoBehaviour
    {
        private const int SiltStripCount = 12;
        private const int EdgeMaskCount = 4;
        private const float BackgroundDistanceMeters = 1.96f;
        private const float HazeDistanceMeters = 1.84f;
        private const float SiltDistanceBaseMeters = 1.70f;
        private const float EdgeMaskDistanceMeters = 1.48f;
        private const float MinimumPresentationDeltaSeconds = 0f;
        private const float MaximumPresentationDeltaSeconds = 0.1f;
        private const string AuthoredStageRootName = "H8_MENU_VISUAL_STAGE_1428";
        private const string AuthoredBackdropName = "Stage_Deep_Backdrop";
        private const string AuthoredHazeName = "Stage_Back_Pressure_Window";
        private static readonly Color AbyssFloorColor = new Color(0.016f, 0.024f, 0.032f, 1f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Authored Atmosphere")]
        [SerializeField, Tooltip("Authored root for menu atmosphere quads. When null, this transform is used; no GameObject is created.")]
        private Transform _atmosphereRoot;
        [SerializeField, Tooltip("Authored full-screen abyss backdrop quad.")]
        private Transform _backdrop;
        [SerializeField, Tooltip("Authored full-screen cyanotic haze quad.")]
        private Transform _haze;
        [SerializeField, Tooltip("Authored pressure silt strip quads. Missing optional entries are skipped without runtime generation.")]
        private Transform[] _siltStrips = new Transform[SiltStripCount];
        [SerializeField, Tooltip("Authored dirty-glass edge mask rail quads. Missing entries are skipped without runtime generation.")]
        private Transform[] _edgeMasks = new Transform[EdgeMaskCount];
        [SerializeField] private MeshRenderer _backdropRenderer;
        [SerializeField] private MeshRenderer _hazeRenderer;
        [SerializeField] private MeshRenderer[] _siltRenderers = new MeshRenderer[SiltStripCount];
        [SerializeField] private MeshRenderer[] _edgeRenderers = new MeshRenderer[EdgeMaskCount];

        private MaterialPropertyBlock _backdropProperties;
        private MaterialPropertyBlock _hazeProperties;
        private MaterialPropertyBlock[] _siltProperties;
        private MaterialPropertyBlock[] _edgeProperties;
        private readonly float[] _siltBaseX = new float[SiltStripCount]; // COLD ALLOC: deterministic fake-current positions.
        private readonly float[] _siltBaseY = new float[SiltStripCount]; // COLD ALLOC: deterministic fake-current positions.
        private readonly float[] _siltPhase = new float[SiltStripCount]; // COLD ALLOC: deterministic fake-current phases.
        private readonly float[] _siltSpeed = new float[SiltStripCount]; // COLD ALLOC: deterministic fake-current rates.

        private Camera _camera;
        private Transform _cameraTransform;
        private bool _configured;

        public void Configure(Camera camera)
        {
            if (camera == null)
                return;

            _camera = camera;
            _cameraTransform = camera.transform;
            if (_cameraTransform == null)
                return;

            EnsurePropertyBlocksCold();
            ResolveAuthoredAtmosphereCold();
            EnsureRootCold();
            EnsureBackdropCold();
            EnsureHazeCold();
            EnsureSiltCold();
            EnsureEdgeMasksCold();
            ApplyCameraClearColor(AbyssFloorColor);
            _configured = true;
        }

        public void Advance(
            float unscaledDeltaTime,
            float now,
            float globalQualityWeight01,
            MenuVisualStyle style,
            MenuVisualConcept concept)
        {
            if (!_configured || _camera == null || _cameraTransform == null)
                return;

            float quality = MenuVisualStyleCatalog.Sanitize01(globalQualityWeight01, 1f);
            float time = math.select(0f, now, math.isfinite(now) & now >= 0f);
            float deltaTime = math.clamp(
                math.select(MinimumPresentationDeltaSeconds, unscaledDeltaTime, math.isfinite(unscaledDeltaTime)),
                MinimumPresentationDeltaSeconds,
                MaximumPresentationDeltaSeconds);

            MenuVisualStyleCatalog.Resolve(style, quality, out MenuVisualStyleState state);
            float conceptBias = ResolveConceptBias(concept);
            ApplyCameraClearColor(ResolveClearColor(in state, quality));
            ApplyBackdrop(in state, quality, conceptBias);
            ApplyHaze(in state, quality, time, deltaTime, conceptBias);
            ApplySilt(in state, quality, time, deltaTime, conceptBias);
            ApplyEdgeMasks(in state, quality, time, conceptBias);
        }

        private void EnsurePropertyBlocksCold()
        {
            if (_backdropProperties == null)
                _backdropProperties = new MaterialPropertyBlock(); // COLD ALLOC: menu backdrop MPB - owner: MainMenuAtmosphereController.
            if (_hazeProperties == null)
                _hazeProperties = new MaterialPropertyBlock(); // COLD ALLOC: menu haze MPB - owner: MainMenuAtmosphereController.
            EnsurePropertyBlockArrayCold(ref _siltProperties, SiltStripCount);
            EnsurePropertyBlockArrayCold(ref _edgeProperties, EdgeMaskCount);
        }

        private static void EnsurePropertyBlockArrayCold(ref MaterialPropertyBlock[] blocks, int count)
        {
            if (blocks == null || blocks.Length != count)
                blocks = new MaterialPropertyBlock[count]; // COLD ALLOC: fixed menu renderer MPB slots.

            for (int i = 0; i < count; i++)
            {
                if (blocks[i] == null)
                    blocks[i] = new MaterialPropertyBlock(); // COLD ALLOC: per-strip menu MPB.
            }
        }

        private void ResolveAuthoredAtmosphereCold()
        {
            EnsureVisualArraysCold();

            Transform searchRoot = _atmosphereRoot;
            if (searchRoot == null || searchRoot == transform)
                searchRoot = FindSceneTransformByNameCold(AuthoredStageRootName);

            if (searchRoot == null)
                searchRoot = transform;

            if (_atmosphereRoot == null || _atmosphereRoot == transform)
                _atmosphereRoot = searchRoot;

            if (_backdrop == null)
                _backdrop = FindChildRecursiveByNameCold(searchRoot, AuthoredBackdropName);
            if (_haze == null)
                _haze = FindChildRecursiveByNameCold(searchRoot, AuthoredHazeName);

            for (int i = 0; i < SiltStripCount; i++)
            {
                if (_siltStrips[i] == null)
                    _siltStrips[i] = FindChildRecursiveByNameCold(searchRoot, ResolveSiltStripName(i));
            }

            for (int i = 0; i < EdgeMaskCount; i++)
            {
                if (_edgeMasks[i] == null)
                    _edgeMasks[i] = FindChildRecursiveByNameCold(searchRoot, ResolveEdgeMaskName(i));
            }
        }

        private void EnsureVisualArraysCold()
        {
            if (_siltStrips == null || _siltStrips.Length != SiltStripCount)
                _siltStrips = new Transform[SiltStripCount]; // COLD ALLOC: fixed authored menu strip references.
            if (_edgeMasks == null || _edgeMasks.Length != EdgeMaskCount)
                _edgeMasks = new Transform[EdgeMaskCount]; // COLD ALLOC: fixed authored menu edge references.
            if (_siltRenderers == null || _siltRenderers.Length != SiltStripCount)
                _siltRenderers = new MeshRenderer[SiltStripCount]; // COLD ALLOC: fixed authored menu strip renderers.
            if (_edgeRenderers == null || _edgeRenderers.Length != EdgeMaskCount)
                _edgeRenderers = new MeshRenderer[EdgeMaskCount]; // COLD ALLOC: fixed authored menu edge renderers.
        }

        private Transform FindSceneTransformByNameCold(string targetName)
        {
            UnityEngine.SceneManagement.Scene scene = gameObject.scene;
            if (!scene.IsValid() || string.IsNullOrEmpty(targetName))
                return null;

            GameObject[] roots = scene.GetRootGameObjects(); // COLD ALLOC: scene bootstrap lookup only.
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                    continue;

                Transform match = FindChildRecursiveByNameCold(root.transform, targetName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static Transform FindChildRecursiveByNameCold(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName))
                return null;

            if (root.name == targetName)
                return root;

            for (int i = 0, childCount = root.childCount; i < childCount; i++)
            {
                Transform match = FindChildRecursiveByNameCold(root.GetChild(i), targetName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static string ResolveSiltStripName(int index)
        {
            switch (index)
            {
                case 0: return "Stage_Action_Cyan_A";
                case 1: return "Stage_Action_Cyan_B";
                case 2: return "Stage_Action_Amber";
                case 3: return "Stage_Deck_Warn_L";
                case 4: return "Stage_Deck_Warn_R";
                case 5: return "Stage_Floor_Plate";
                case 6: return "Stage_Command_Pedestal";
                default: return null;
            }
        }

        private static string ResolveEdgeMaskName(int index)
        {
            switch (index)
            {
                case 0: return "Stage_Left_Rail";
                case 1: return "Stage_Right_Rail";
                case 2: return "Stage_Top_Rail";
                case 3: return "Stage_Bottom_Rail";
                default: return null;
            }
        }

        private void EnsureRootCold()
        {
            if (_atmosphereRoot == null)
                _atmosphereRoot = transform;

            _atmosphereRoot.SetParent(_cameraTransform, false);
            _atmosphereRoot.localPosition = Vector3.zero;
            _atmosphereRoot.localRotation = Quaternion.identity;
            _atmosphereRoot.localScale = Vector3.one;
        }

        private void EnsureBackdropCold()
        {
            if (_backdrop != null)
            {
                _backdropRenderer = ResolveAtmosphereRenderer(_backdrop, _backdropRenderer);
                ConfigureAtmosphereRenderer(_backdropRenderer);
                _backdrop.localPosition = new Vector3(0f, 0f, BackgroundDistanceMeters);
                _backdrop.localRotation = Quaternion.Euler(0f, 180f, 0f);
                _backdrop.localScale = new Vector3(3.25f, 1.94f, 1f);
                return;
            }

            _backdropRenderer = null;
        }

        private void EnsureHazeCold()
        {
            if (_haze != null)
            {
                _hazeRenderer = ResolveAtmosphereRenderer(_haze, _hazeRenderer);
                ConfigureAtmosphereRenderer(_hazeRenderer);
                _haze.localPosition = new Vector3(0f, -0.05f, HazeDistanceMeters);
                _haze.localRotation = Quaternion.Euler(0f, 180f, 0f);
                _haze.localScale = new Vector3(3.05f, 1.38f, 1f);
                return;
            }

            _hazeRenderer = null;
        }

        private void EnsureSiltCold()
        {
            if (!HasArrayCapacity(_siltStrips, SiltStripCount) || !HasArrayCapacity(_siltRenderers, SiltStripCount))
                return;

            for (int i = 0; i < SiltStripCount; i++)
            {
                Transform strip = GetArrayValue(_siltStrips, i);
                if (strip == null)
                    continue;

                _siltRenderers[i] = ResolveAtmosphereRenderer(strip, GetArrayValue(_siltRenderers, i));
                ConfigureAtmosphereRenderer(_siltRenderers[i]);

                float lane = i / (float)(SiltStripCount - 1);
                _siltBaseX[i] = math.lerp(-1.52f, 1.52f, Frac01((i * 0.371f) + 0.17f));
                _siltBaseY[i] = math.lerp(-0.78f, 0.78f, lane);
                _siltPhase[i] = (i * 1.6180339f) + 0.37f;
                _siltSpeed[i] = math.lerp(0.19f, 0.56f, Frac01((i * 0.281f) + 0.09f));
            }
        }

        private void EnsureEdgeMasksCold()
        {
            if (!HasArrayCapacity(_edgeMasks, EdgeMaskCount) || !HasArrayCapacity(_edgeRenderers, EdgeMaskCount))
                return;

            for (int i = 0; i < EdgeMaskCount; i++)
            {
                Transform mask = GetArrayValue(_edgeMasks, i);
                if (mask == null)
                    continue;

                _edgeRenderers[i] = ResolveAtmosphereRenderer(mask, GetArrayValue(_edgeRenderers, i));
                ConfigureAtmosphereRenderer(_edgeRenderers[i]);
            }

            SetEdgeMaskPose(0, new Vector3(-1.52f, 0f, EdgeMaskDistanceMeters), new Vector3(0.44f, 2.10f, 1f));
            SetEdgeMaskPose(1, new Vector3(1.55f, 0f, EdgeMaskDistanceMeters), new Vector3(0.38f, 2.10f, 1f));
            SetEdgeMaskPose(2, new Vector3(0f, 0.91f, EdgeMaskDistanceMeters), new Vector3(3.35f, 0.34f, 1f));
            SetEdgeMaskPose(3, new Vector3(0f, -0.88f, EdgeMaskDistanceMeters), new Vector3(3.35f, 0.36f, 1f));
        }

        private static T GetArrayValue<T>(T[] values, int index) where T : class
        {
            return values != null && (uint)index < (uint)values.Length ? values[index] : null;
        }

        private static bool HasArrayCapacity<T>(T[] values, int required)
        {
            return values != null && values.Length >= required;
        }

        private static MeshRenderer ResolveAtmosphereRenderer(Transform visual, MeshRenderer configured)
        {
            if (configured != null)
                return configured;

            if (visual != null && visual.TryGetComponent(out MeshRenderer renderer))
                return renderer;

            return null;
        }

        private static void ConfigureAtmosphereRenderer(MeshRenderer renderer)
        {
            if (renderer == null)
                return;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
        }

        private static void SetRendererColor(MeshRenderer renderer, MaterialPropertyBlock propertyBlock, Color color)
        {
            if (renderer == null || propertyBlock == null)
                return;

            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyCameraClearColor(Color color)
        {
            if (_camera == null)
                return;

            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = ResolveAbyssSafe(color);
        }

        private void ApplyBackdrop(in MenuVisualStyleState state, float quality, float conceptBias)
        {
            if (_backdrop == null)
                return;

            Color deep = ResolveAbyssSafe(LerpColor(AbyssFloorColor, state.BackgroundColor, 0.30f + quality * 0.26f));
            deep.a = math.lerp(0.40f, 0.58f, quality) + conceptBias * 0.04f;
            SetRendererColor(_backdropRenderer, _backdropProperties, deep);
            _backdrop.localScale = new Vector3(3.20f + quality * 0.18f, 1.90f + quality * 0.08f, 1f);
            if (_backdropRenderer != null)
                _backdropRenderer.enabled = true;
        }

        private void ApplyHaze(in MenuVisualStyleState state, float quality, float now, float deltaTime, float conceptBias)
        {
            if (_haze == null)
                return;

            float crawl = math.sin((now * 0.31f) + conceptBias * 4.2f);
            float lift = math.cos((now * 0.19f) + 1.7f) * (0.018f + quality * 0.028f);
            _haze.localPosition = new Vector3(crawl * (0.035f + quality * 0.06f), -0.055f + lift, HazeDistanceMeters);
            _haze.localScale = new Vector3(2.86f + quality * 0.52f, 1.16f + quality * 0.42f, 1f);

            Color haze = LerpColor(state.AccentColor, state.SecondaryTextColor, 0.45f + state.WetGlassWeight * 0.22f);
            haze = ResolveAbyssSafe(haze);
            haze.a = (0.035f + quality * 0.075f) * (0.65f + state.WetGlassWeight * 0.75f + conceptBias * 0.32f);
            SetRendererColor(_hazeRenderer, _hazeProperties, haze);
            if (_hazeRenderer != null)
                _hazeRenderer.enabled = deltaTime >= 0f;
        }

        private void ApplySilt(in MenuVisualStyleState state, float quality, float now, float deltaTime, float conceptBias)
        {
            int activeCount = math.clamp((int)math.ceil(math.lerp(4f, SiltStripCount, quality)), 0, SiltStripCount);
            float depthAlpha = 0.018f + quality * 0.055f + state.ScanlineWeight * 0.018f;
            float speedScale = 0.45f + quality * 0.80f + conceptBias * 0.20f;

            for (int i = 0; i < SiltStripCount; i++)
            {
                MeshRenderer renderer = GetArrayValue(_siltRenderers, i);
                Transform strip = GetArrayValue(_siltStrips, i);
                if (renderer == null || strip == null)
                    continue;

                bool visible = i < activeCount;
                renderer.enabled = visible;
                if (!visible)
                    continue;

                float phase = _siltPhase[i];
                float wave = math.sin((now * _siltSpeed[i] * speedScale) + phase);
                float counter = math.cos((now * (_siltSpeed[i] * 0.47f + 0.11f)) + phase * 0.77f);
                float x = _siltBaseX[i] + wave * (0.035f + quality * 0.11f);
                float y = _siltBaseY[i] + counter * (0.012f + quality * 0.045f);
                float z = SiltDistanceBaseMeters + (i % 4) * 0.028f;
                strip.localPosition = new Vector3(x, y, z);
                strip.localRotation = Quaternion.Euler(0f, 180f, wave * (1.4f + quality * 2.8f));
                strip.localScale = new Vector3(
                    math.lerp(0.42f, 1.05f, Frac01(phase * 0.37f)) * (0.50f + quality * 0.55f),
                    math.lerp(0.006f, 0.018f, Frac01(phase * 0.61f)) * (1f + quality * 1.4f),
                    1f);

                Color color = (i & 1) == 0
                    ? LerpColor(state.AccentColor, state.PrimaryTextColor, 0.36f)
                    : LerpColor(state.WarningColor, state.AccentColor, 0.72f);
                color = ResolveAbyssSafe(color);
                color.a = depthAlpha * math.lerp(0.55f, 1.0f, Frac01(phase * 0.19f));
                SetRendererColor(renderer, GetArrayValue(_siltProperties, i), color);
            }
        }

        private void ApplyEdgeMasks(in MenuVisualStyleState state, float quality, float now, float conceptBias)
        {
            float pulse = 0.5f + math.sin((now * 0.42f) + conceptBias * 2.7f) * 0.5f;
            Color color = ResolveAbyssSafe(LerpColor(AbyssFloorColor, state.PanelColor, 0.18f + quality * 0.16f));
            color.a = 0.30f + state.WetGlassWeight * 0.07f + pulse * (0.012f + quality * 0.024f);

            for (int i = 0; i < EdgeMaskCount; i++)
            {
                MeshRenderer renderer = GetArrayValue(_edgeRenderers, i);
                if (renderer != null)
                    renderer.enabled = true;
                SetRendererColor(renderer, GetArrayValue(_edgeProperties, i), color);
            }
        }

        private void SetEdgeMaskPose(int index, Vector3 localPosition, Vector3 localScale)
        {
            Transform mask = GetArrayValue(_edgeMasks, index);
            if ((uint)index >= EdgeMaskCount || mask == null)
                return;

            mask.localPosition = localPosition;
            mask.localRotation = Quaternion.Euler(0f, 180f, 0f);
            mask.localScale = localScale;
        }

        private static Color ResolveClearColor(in MenuVisualStyleState state, float quality)
        {
            Color color = LerpColor(AbyssFloorColor, state.BackgroundColor, 0.20f + quality * 0.30f);
            color.a = 1f;
            return ResolveAbyssSafe(color);
        }

        private static Color ResolveAbyssSafe(Color color)
        {
            return new Color(
                math.max(AbyssFloorColor.r, math.select(AbyssFloorColor.r, color.r, math.isfinite(color.r))),
                math.max(AbyssFloorColor.g, math.select(AbyssFloorColor.g, color.g, math.isfinite(color.g))),
                math.max(AbyssFloorColor.b, math.select(AbyssFloorColor.b, color.b, math.isfinite(color.b))),
                math.saturate(math.select(1f, color.a, math.isfinite(color.a))));
        }

        private static float ResolveConceptBias(MenuVisualConcept concept)
        {
            int index = math.clamp((int)concept, 0, MenuVisualConceptCatalog.ConceptCount - 1);
            return (index + 1) * (1f / MenuVisualConceptCatalog.ConceptCount);
        }

        private static Color LerpColor(Color a, Color b, float t)
        {
            float x = math.saturate(math.select(0f, t, math.isfinite(t)));
            return new Color(
                math.lerp(a.r, b.r, x),
                math.lerp(a.g, b.g, x),
                math.lerp(a.b, b.b, x),
                math.lerp(a.a, b.a, x));
        }

        private static float Frac01(float value)
        {
            return value - math.floor(value);
        }

        private void OnDestroy()
        {
            _backdropProperties?.Clear();
            _hazeProperties?.Clear();
            for (int i = 0; i < SiltStripCount; i++)
                GetArrayValue(_siltProperties, i)?.Clear();
            for (int i = 0; i < EdgeMaskCount; i++)
                GetArrayValue(_edgeProperties, i)?.Clear();
        }
    }
}
