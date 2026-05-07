// ============================================================================
// HECTON-8 - BaseModuleCondensationSurface.cs
// Authored shared-material bridge for hot interior / cold hull condensation.
// ============================================================================

using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/Base Module Condensation Surface")]
    public sealed class BaseModuleCondensationSurface : MonoBehaviour
    {
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
        private static readonly int DetailNormalMapId = Shader.PropertyToID("_DetailNormalMap");
        private static readonly int CondensationStrengthId = Shader.PropertyToID("_CondensationStrength");

        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Material dryMaterial;
        [SerializeField] private Material condensationMaterial;
        [SerializeField] private Texture drippingNormalMap;
        [SerializeField, Range(0f, 1f)] private float wetSmoothness = 0.92f;
        [SerializeField, Range(0f, 1f)] private float condensationStrength = 1f;

        private bool _active;

        private void Awake()
        {
            CacheReferences();
            CaptureDryMaterialIfMissing();
            ConfigureCondensationMaterial();
            ApplyCondensation(false);
        }

        internal void ApplyCondensation(bool active)
        {
            CacheReferences();
            if (targetRenderer == null)
                return;

            Material targetMaterial = active && condensationMaterial != null
                ? condensationMaterial
                : dryMaterial;
            if (targetMaterial == null)
                return;

            if (active)
                ConfigureCondensationMaterial();

            if (!ReferenceEquals(targetRenderer.sharedMaterial, targetMaterial))
                targetRenderer.sharedMaterial = targetMaterial;
            _active = active;
        }

        private void CacheReferences()
        {
            if (targetRenderer == null)
                TryGetComponent(out targetRenderer);
        }

        private void CaptureDryMaterialIfMissing()
        {
            if (dryMaterial == null && targetRenderer != null)
                dryMaterial = targetRenderer.sharedMaterial;
        }

        private void ConfigureCondensationMaterial()
        {
            if (condensationMaterial == null)
                return;

            if (condensationMaterial.HasProperty(SmoothnessId))
                condensationMaterial.SetFloat(SmoothnessId, wetSmoothness);

            if (drippingNormalMap != null)
            {
                if (condensationMaterial.HasProperty(BumpMapId))
                    condensationMaterial.SetTexture(BumpMapId, drippingNormalMap);
                if (condensationMaterial.HasProperty(NormalMapId))
                    condensationMaterial.SetTexture(NormalMapId, drippingNormalMap);
                if (condensationMaterial.HasProperty(DetailNormalMapId))
                    condensationMaterial.SetTexture(DetailNormalMapId, drippingNormalMap);
            }

            if (condensationMaterial.HasProperty(CondensationStrengthId))
                condensationMaterial.SetFloat(CondensationStrengthId, condensationStrength);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheReferences();
            wetSmoothness = Mathf.Clamp01(wetSmoothness);
            condensationStrength = Mathf.Clamp01(condensationStrength);
            ConfigureCondensationMaterial();
            if (!Application.isPlaying && _active)
                ApplyCondensation(true);
        }
#endif
    }
}
