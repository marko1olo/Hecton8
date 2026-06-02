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
        private static readonly Color AbyssFloorColor = new Color(0.016f, 0.024f, 0.032f, 1f);

        private readonly Transform[] _siltStrips = new Transform[SiltStripCount]; // COLD ALLOC: fixed menu atmosphere strip cache.
        private readonly MeshRenderer[] _siltRenderers = new MeshRenderer[SiltStripCount]; // COLD ALLOC: no renderer lookup in visual sync.
        private readonly Material[] _siltMaterials = new Material[SiltStripCount]; // COLD ALLOC: per-strip alpha without MaterialPropertyBlock churn.
        private readonly float[] _siltBaseX = new float[SiltStripCount]; // COLD ALLOC: deterministic fake-current positions.
        private readonly float[] _siltBaseY = new float[SiltStripCount]; // COLD ALLOC: deterministic fake-current positions.
        private readonly float[] _siltPhase = new float[SiltStripCount]; // COLD ALLOC: deterministic fake-current phases.
        private readonly float[] _siltSpeed = new float[SiltStripCount]; // COLD ALLOC: deterministic fake-current rates.
        private readonly Transform[] _edgeMasks = new Transform[EdgeMaskCount]; // COLD ALLOC: fixed dirty-glass edge masks.
        private readonly MeshRenderer[] _edgeRenderers = new MeshRenderer[EdgeMaskCount]; // COLD ALLOC: no lookup in LateFrameTick.
        private readonly Material[] _edgeMaterials = new Material[EdgeMaskCount]; // COLD ALLOC: edge color cache.

        private Camera _camera;
        private Transform _cameraTransform;
        private Transform _atmosphereRoot;
        private Transform _backdrop;
        private Transform _haze;
        private MeshRenderer _backdropRenderer;
        private MeshRenderer _hazeRenderer;
        private Material _backdropMaterial;
        private Material _hazeMaterial;
        private Shader _transparentShader;
        private bool _configured;

        public void Configure(Camera camera)
        {
            if (camera == null)
                return;

            _camera = camera;
            _cameraTransform = camera.transform;
            if (_cameraTransform == null)
                return;

            EnsureTransparentShaderCold();
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

        private void EnsureTransparentShaderCold()
        {
            if (_transparentShader != null)
                return;

            _transparentShader = Shader.Find("Sprites/Default");
            if (_transparentShader == null)
                _transparentShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (_transparentShader == null)
                _transparentShader = Shader.Find("Unlit/Color");
        }

        private void EnsureRootCold()
        {
            if (_atmosphereRoot != null)
                return;

            GameObject root = new GameObject("H8_MenuCamera_Atmosphere"); // COLD ALLOC: presentation-only generated camera layer.
            root.hideFlags = HideFlags.DontSave;
            _atmosphereRoot = root.transform;
            _atmosphereRoot.SetParent(_cameraTransform, false);
            _atmosphereRoot.localPosition = Vector3.zero;
            _atmosphereRoot.localRotation = Quaternion.identity;
            _atmosphereRoot.localScale = Vector3.one;
        }

        private void EnsureBackdropCold()
        {
            if (_backdrop != null)
                return;

            _backdrop = CreateQuadCold("H8_Menu_Abyss_Backdrop", _atmosphereRoot, out _backdropRenderer, out _backdropMaterial);
            _backdrop.localPosition = new Vector3(0f, 0f, BackgroundDistanceMeters);
            _backdrop.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _backdrop.localScale = new Vector3(3.25f, 1.94f, 1f);
        }

        private void EnsureHazeCold()
        {
            if (_haze != null)
                return;

            _haze = CreateQuadCold("H8_Menu_Cyanotic_Haze", _atmosphereRoot, out _hazeRenderer, out _hazeMaterial);
            _haze.localPosition = new Vector3(0f, -0.05f, HazeDistanceMeters);
            _haze.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _haze.localScale = new Vector3(3.05f, 1.38f, 1f);
        }

        private void EnsureSiltCold()
        {
            for (int i = 0; i < SiltStripCount; i++)
            {
                if (_siltStrips[i] != null)
                    continue;

                Transform strip = CreateQuadCold("H8_Menu_Pressure_Silt_" + i.ToString("00"), _atmosphereRoot, out MeshRenderer renderer, out Material material);
                _siltStrips[i] = strip;
                _siltRenderers[i] = renderer;
                _siltMaterials[i] = material;

                float lane = i / (float)(SiltStripCount - 1);
                _siltBaseX[i] = math.lerp(-1.52f, 1.52f, Frac01((i * 0.371f) + 0.17f));
                _siltBaseY[i] = math.lerp(-0.78f, 0.78f, lane);
                _siltPhase[i] = (i * 1.6180339f) + 0.37f;
                _siltSpeed[i] = math.lerp(0.19f, 0.56f, Frac01((i * 0.281f) + 0.09f));
            }
        }

        private void EnsureEdgeMasksCold()
        {
            for (int i = 0; i < EdgeMaskCount; i++)
            {
                if (_edgeMasks[i] != null)
                    continue;

                Transform mask = CreateQuadCold("H8_Menu_Glass_Edge_" + i.ToString("00"), _atmosphereRoot, out MeshRenderer renderer, out Material material);
                _edgeMasks[i] = mask;
                _edgeRenderers[i] = renderer;
                _edgeMaterials[i] = material;
            }

            SetEdgeMaskPose(0, new Vector3(-1.52f, 0f, EdgeMaskDistanceMeters), new Vector3(0.44f, 2.10f, 1f));
            SetEdgeMaskPose(1, new Vector3(1.55f, 0f, EdgeMaskDistanceMeters), new Vector3(0.38f, 2.10f, 1f));
            SetEdgeMaskPose(2, new Vector3(0f, 0.91f, EdgeMaskDistanceMeters), new Vector3(3.35f, 0.34f, 1f));
            SetEdgeMaskPose(3, new Vector3(0f, -0.88f, EdgeMaskDistanceMeters), new Vector3(3.35f, 0.36f, 1f));
        }

        private Transform CreateQuadCold(string objectName, Transform parent, out MeshRenderer renderer, out Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad); // COLD ALLOC: fixed menu visual fake, never spawned per frame.
            quad.name = objectName;
            quad.hideFlags = HideFlags.DontSave;
            quad.layer = _camera != null ? _camera.gameObject.layer : quad.layer;
            Transform quadTransform = quad.transform;
            quadTransform.SetParent(parent, false);

            if (quad.TryGetComponent(out Collider collider))
                collider.enabled = false;

            renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;

            material = CreateTransparentMaterialCold(objectName + "_Material");
            renderer.sharedMaterial = material;
            return quadTransform;
        }

        private Material CreateTransparentMaterialCold(string materialName)
        {
            Material material = _transparentShader != null
                ? new Material(_transparentShader) // COLD ALLOC: menu atmosphere generated material.
                : new Material(Shader.Find("Unlit/Color")); // COLD ALLOC: last-resort generated material.
            material.name = materialName;
            material.hideFlags = HideFlags.DontSave;
            material.renderQueue = (int)RenderQueue.Transparent;
            ConfigureTransparentMaterial(material);
            return material;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
                return;

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            material.color = color;
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
            SetMaterialColor(_backdropMaterial, deep);
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
            SetMaterialColor(_hazeMaterial, haze);
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
                MeshRenderer renderer = _siltRenderers[i];
                Transform strip = _siltStrips[i];
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
                SetMaterialColor(_siltMaterials[i], color);
            }
        }

        private void ApplyEdgeMasks(in MenuVisualStyleState state, float quality, float now, float conceptBias)
        {
            float pulse = 0.5f + math.sin((now * 0.42f) + conceptBias * 2.7f) * 0.5f;
            Color color = ResolveAbyssSafe(LerpColor(AbyssFloorColor, state.PanelColor, 0.18f + quality * 0.16f));
            color.a = 0.30f + state.WetGlassWeight * 0.07f + pulse * (0.012f + quality * 0.024f);

            for (int i = 0; i < EdgeMaskCount; i++)
            {
                if (_edgeRenderers[i] != null)
                    _edgeRenderers[i].enabled = true;
                SetMaterialColor(_edgeMaterials[i], color);
            }
        }

        private void SetEdgeMaskPose(int index, Vector3 localPosition, Vector3 localScale)
        {
            if ((uint)index >= EdgeMaskCount || _edgeMasks[index] == null)
                return;

            _edgeMasks[index].localPosition = localPosition;
            _edgeMasks[index].localRotation = Quaternion.Euler(0f, 180f, 0f);
            _edgeMasks[index].localScale = localScale;
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
            DestroyMaterialCold(_backdropMaterial);
            DestroyMaterialCold(_hazeMaterial);
            for (int i = 0; i < SiltStripCount; i++)
                DestroyMaterialCold(_siltMaterials[i]);
            for (int i = 0; i < EdgeMaskCount; i++)
                DestroyMaterialCold(_edgeMaterials[i]);
        }

        private static void DestroyMaterialCold(Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }
    }
}
