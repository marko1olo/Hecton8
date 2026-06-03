namespace Hecton8.Inventory
{
    using System;
    using UnityEngine;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Inventory/Emission Binding")]
    public sealed class InventoryEmissionStatePresenter : MonoBehaviour
    {
        [SerializeField] private MeshRenderer[] renderers = Array.Empty<MeshRenderer>();
        [SerializeField] private float authoredQualityWeight = 1f;
        [SerializeField] private float minimumPulseQuality = 0.35f;
        [SerializeField] private float baseEmissionStrength;
        [SerializeField] private float pulseEmissionStrength = 0.65f;
        [SerializeField] private float pulseFrequencyHz = 0.55f;

        public int RendererCount => renderers != null ? renderers.Length : 0;
        public float AuthoredQualityWeight => authoredQualityWeight;
        public float MinimumPulseQuality => minimumPulseQuality;
        public float BaseEmissionStrength => baseEmissionStrength;
        public float PulseEmissionStrength => pulseEmissionStrength;
        public float PulseFrequencyHz => pulseFrequencyHz;
        public bool HasValidBinding =>
            HasCompleteRendererBindings() &&
            IsUnitInterval(authoredQualityWeight) &&
            IsUnitInterval(minimumPulseQuality) &&
            IsNonNegativeFinite(baseEmissionStrength) &&
            IsNonNegativeFinite(pulseEmissionStrength) &&
            IsFinite(pulseFrequencyHz) &&
            pulseFrequencyHz >= 0.05f &&
            pulseFrequencyHz <= 4f;

#if UNITY_EDITOR
        public void ConfigureEditorBake(
            MeshRenderer[] authoredRenderers,
            float authoredGlobalQualityWeight,
            float authoredBaseEmissionStrength,
            float authoredPulseEmissionStrength,
            float authoredPulseFrequencyHz,
            float authoredMinimumPulseQuality)
        {
            renderers = authoredRenderers ?? Array.Empty<MeshRenderer>();
            authoredQualityWeight = SaturateFinite(authoredGlobalQualityWeight, 1f);
            baseEmissionStrength = Mathf.Max(0f, FiniteOr(authoredBaseEmissionStrength, 0f));
            pulseEmissionStrength = Mathf.Max(0f, FiniteOr(authoredPulseEmissionStrength, 0.65f));
            pulseFrequencyHz = Mathf.Clamp(FiniteOr(authoredPulseFrequencyHz, 0.55f), 0.05f, 4f);
            minimumPulseQuality = Mathf.Clamp01(FiniteOr(authoredMinimumPulseQuality, 0.35f));
        }
#endif

        private bool HasCompleteRendererBindings()
        {
            MeshRenderer[] boundRenderers = renderers;
            if (boundRenderers == null || boundRenderers.Length == 0)
                return false;

            for (int i = 0; i < boundRenderers.Length; i++)
            {
                if (boundRenderers[i] == null)
                    return false;
            }

            return true;
        }

        private static float SaturateFinite(float value, float fallback)
        {
            return Mathf.Clamp01(IsFinite(value) ? value : fallback);
        }

        private static float FiniteOr(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsNonNegativeFinite(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        private static bool IsUnitInterval(float value)
        {
            return IsFinite(value) && value >= 0f && value <= 1f;
        }
    }
}
