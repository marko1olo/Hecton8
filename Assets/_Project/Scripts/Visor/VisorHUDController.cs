// File: Scripts/Visor/VisorHUDController.cs
using UnityEngine;

namespace NASAPunk.Visor
{
    [ExecuteAlways]
    public class VisorHUDController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Renderer _visorRenderer;
        [SerializeField] private Camera _hudCamera;

        [Header("HUD Render Texture Settings")]
        [SerializeField] private int _rtWidth = 1024;
        [SerializeField] private int _rtHeight = 1024;
        [SerializeField] private FilterMode _filterMode = FilterMode.Bilinear;

        [Header("Runtime Tuning")]
        [SerializeField, Range(0f, 5f)] private float _hudIntensity = 2.5f;
        [SerializeField] private Color _hudTint = new Color(0.2f, 1f, 0.3f, 1f);
        [SerializeField, Range(0f, 2f)] private float _scratchBleed = 0.8f;
        [SerializeField, Range(0f, 0.1f)] private float _distortion = 0.02f;

        private RenderTexture _hudRT;
        private MaterialPropertyBlock _mpb;

        // Shader property IDs (cached)
        private static readonly int ID_HUDTex = Shader.PropertyToID("_HUD_RenderTexture");
        private static readonly int ID_HUDIntensity = Shader.PropertyToID("_HUD_Intensity");
        private static readonly int ID_HUDColor = Shader.PropertyToID("_HUD_Color");
        private static readonly int ID_ScratchBleed = Shader.PropertyToID("_HUD_ScratchBleed");
        private static readonly int ID_Distortion = Shader.PropertyToID("_DistortionStrength");

        private void OnEnable()
        {
            _mpb = new MaterialPropertyBlock();
            CreateRT();
            BindRT();
        }

        private void OnDisable()
        {
            ReleaseRT();
        }

        private void Update()
        {
            if (_visorRenderer == null) return;

            _visorRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(ID_HUDIntensity, _hudIntensity);
            _mpb.SetColor(ID_HUDColor, _hudTint);
            _mpb.SetFloat(ID_ScratchBleed, _scratchBleed);
            _mpb.SetFloat(ID_Distortion, _distortion);
            _visorRenderer.SetPropertyBlock(_mpb);
        }

        private void CreateRT()
        {
            _hudRT = new RenderTexture(_rtWidth, _rtHeight, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = _filterMode,
                useMipMap = false,
                name = "VisorHUD_RT"
            };
            _hudRT.Create();
        }

        private void BindRT()
        {
            if (_hudCamera != null)
            {
                _hudCamera.targetTexture = _hudRT;
            }

            if (_visorRenderer != null)
            {
                _visorRenderer.GetPropertyBlock(_mpb);
                _mpb.SetTexture(ID_HUDTex, _hudRT);
                _visorRenderer.SetPropertyBlock(_mpb);
            }
        }

        private void ReleaseRT()
        {
            if (_hudCamera != null)
                _hudCamera.targetTexture = null;

            if (_hudRT != null)
            {
                _hudRT.Release();
                DestroyImmediate(_hudRT);
            }
        }

        /// <summary>
        /// Вызывается при смене режима HUD (напр. при переключении на карту)
        /// </summary>
        public void SetHUDIntensity(float intensity)
        {
            _hudIntensity = Mathf.Clamp(intensity, 0f, 5f);
        }

        /// <summary>
        /// «Зависание» HUD — мерцание
        /// </summary>
        public void GlitchPulse(float duration = 0.3f)
        {
            StartCoroutine(GlitchCoroutine(duration));
        }

        private System.Collections.IEnumerator GlitchCoroutine(float dur)
        {
            float original = _hudIntensity;
            float elapsed = 0;
            while (elapsed < dur)
            {
                _hudIntensity = original * Random.Range(0.1f, 2f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            _hudIntensity = original;
        }
    }
}