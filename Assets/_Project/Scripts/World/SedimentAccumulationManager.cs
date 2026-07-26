using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Publishes fallback and procedural shader sediment parameters (_HectonSedimentOverlayParamsA/B, tints)
    /// to global URP materials. Provides a clear integration boundary for dynamic erosion compute shaders via SetSedimentMaskMap.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)]
    public sealed class SedimentAccumulationManager : MonoBehaviour
    {
        private static readonly int _SedimentMaskTextureId = Shader.PropertyToID("_HectonSedimentMaskTex");
        private static readonly int _SedimentWorldRectId = Shader.PropertyToID("_HectonSedimentWorldRect");
        private static readonly int _SedimentOverlayParamsAId = Shader.PropertyToID("_HectonSedimentOverlayParamsA");
        private static readonly int _SedimentOverlayParamsBId = Shader.PropertyToID("_HectonSedimentOverlayParamsB");
        private static readonly int _SedimentTintAId = Shader.PropertyToID("_HectonSedimentTintA");
        private static readonly int _SedimentTintBId = Shader.PropertyToID("_HectonSedimentTintB");

        [Header("Sediment Shader Response")]
        [SerializeField, Range(0f, 1f), Tooltip("Minimum up-facing normal Y required to show sediment.")]
        private float upFacingThreshold = 0.7f;
        [SerializeField, Range(0.01f, 1f), Tooltip("Pure shader sediment coverage strength.")]
        private float overlayIntensity = 0.9f;
        [SerializeField, Range(0.02f, 0.35f), Tooltip("World-space ripple frequency used by the procedural sand normal.")]
        private float rippleScale = 0.11f;
        [SerializeField, Range(0.05f, 1f), Tooltip("Strength of the procedural dune normal blended over lit surfaces.")]
        private float rippleNormalStrength = 0.32f;
        [SerializeField, Range(0f, 0.2f), Tooltip("Metallic target when fully covered by sediment.")]
        private float sedimentMetallic = 0.03f;
        [SerializeField, Range(0f, 1f), Tooltip("Smoothness target when fully covered by sediment.")]
        private float sedimentSmoothness = 0.28f;
        [SerializeField, Tooltip("Primary silt color blended into exposed surfaces.")]
        private Color sedimentTintA = new Color(0.71f, 0.67f, 0.58f, 1f);
        [SerializeField, Tooltip("Secondary dune tint used for low-frequency ripple variation.")]
        private Color sedimentTintB = new Color(0.57f, 0.54f, 0.47f, 1f);

        [Header("Debug")]
        [SerializeField, Tooltip("Current normalized coverage strength used by shader overlays.")]
        private float _debugOverlayIntensity;

        private Texture _activeMaskTexture;
        private Vector4 _activeWorldRect;

        private void Awake()
        {
            PublishGlobals();
        }

        private void OnEnable()
        {
            PublishGlobals();
        }

        private void OnDisable()
        {
            PublishFallbackGlobals();
        }

        private void OnDestroy()
        {
            PublishFallbackGlobals();
        }

        /// <summary>
        /// Dynamically binds active compute sediment mask texture and world space UV rectangle bounds.
        /// </summary>
        /// <param name="maskTex">Active compute sediment accumulation texture (e.g. R16_UNORM/R8_UNORM).</param>
        /// <param name="worldRect">World bounds Vector4 (minX, minZ, width, height).</param>
        public void SetSedimentMaskMap(Texture maskTex, Vector4 worldRect)
        {
            _activeMaskTexture = maskTex;
            _activeWorldRect = worldRect;
            if (isActiveAndEnabled)
            {
                PublishGlobals();
            }
        }

        private void PublishGlobals()
        {
            float threshold = math.saturate(upFacingThreshold);
            Texture maskTex = _activeMaskTexture != null ? _activeMaskTexture : Texture2D.blackTexture;
            Vector4 worldRect = _activeMaskTexture != null ? _activeWorldRect : Vector4.zero;

            Shader.SetGlobalTexture(_SedimentMaskTextureId, maskTex);
            Shader.SetGlobalVector(_SedimentWorldRectId, worldRect);
            Shader.SetGlobalVector(
                _SedimentOverlayParamsAId,
                new Vector4(
                    1f,
                    threshold,
                    1f / math.max(0.001f, 1f - threshold),
                    math.max(0.001f, rippleScale)));
            Shader.SetGlobalVector(
                _SedimentOverlayParamsBId,
                new Vector4(
                    math.max(0f, rippleNormalStrength),
                    math.max(0f, sedimentMetallic),
                    math.saturate(sedimentSmoothness),
                    math.saturate(overlayIntensity)));
            Shader.SetGlobalColor(_SedimentTintAId, sedimentTintA.linear);
            Shader.SetGlobalColor(_SedimentTintBId, sedimentTintB.linear);
            _debugOverlayIntensity = math.saturate(overlayIntensity);
        }

        private void PublishFallbackGlobals()
        {
            Shader.SetGlobalTexture(_SedimentMaskTextureId, Texture2D.blackTexture);
            Shader.SetGlobalVector(_SedimentWorldRectId, Vector4.zero);
            Shader.SetGlobalVector(_SedimentOverlayParamsAId, Vector4.zero);
            Shader.SetGlobalVector(_SedimentOverlayParamsBId, Vector4.zero);
            Shader.SetGlobalColor(_SedimentTintAId, Color.black);
            Shader.SetGlobalColor(_SedimentTintBId, Color.black);
            _debugOverlayIntensity = 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            upFacingThreshold = Mathf.Clamp01(upFacingThreshold);
            overlayIntensity = Mathf.Clamp01(overlayIntensity);
            rippleScale = Mathf.Clamp(rippleScale, 0.02f, 0.35f);
            rippleNormalStrength = Mathf.Clamp(rippleNormalStrength, 0.05f, 1f);
            sedimentMetallic = Mathf.Clamp(sedimentMetallic, 0f, 0.2f);
            sedimentSmoothness = Mathf.Clamp01(sedimentSmoothness);
            if (isActiveAndEnabled)
                PublishGlobals();
        }
#endif
    }
}

