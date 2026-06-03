using System;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Power/Power Status Emissive Binding")]
    public sealed class PowerStatusEmissiveBinding : MonoBehaviour
    {
        private const int LowTierQualityQuantization = 64;
        private const int HighTierQualityQuantization = 512;

        [SerializeField] private Renderer[] statusRenderers = Array.Empty<Renderer>();
        [SerializeField] private string emissionColorProperty = "_EmissionColor";
        [SerializeField] private string emissionStrengthProperty = "_EmissionStrength";
        [SerializeField] private string globalQualityProperty = "_H8GlobalQualityWeight";
        [SerializeField] private Color baseEmission = new Color(0.04f, 0.35f, 0.22f, 1f);
        [SerializeField] private Color failureEmission = new Color(1f, 0.12f, 0.04f, 1f);
        [SerializeField, Min(0f)] private float minEmissionStrength = 0.15f;
        [SerializeField, Min(0f)] private float maxEmissionStrength = 4f;
        [SerializeField, Min(0f)] private float pulseStrength = 0.65f;

        private MaterialPropertyBlock _propertyBlock;
        private int _emissionColorId;
        private int _emissionStrengthId;
        private int _globalQualityId;
        private int _lastQuantizedState = int.MinValue;

        public int RendererCount => statusRenderers != null ? statusRenderers.Length : 0;

        public bool TryGetRenderer(int index, out Renderer target)
        {
            Renderer[] source = statusRenderers;
            if (source == null || (uint)index >= (uint)source.Length)
            {
                target = null;
                return false;
            }

            target = source[index];
            return target != null;
        }

#if UNITY_EDITOR
        public void ConfigureEditorBake(
            Renderer[] renderers,
            string emissionColorPropertyName,
            string emissionStrengthPropertyName,
            string qualityPropertyName,
            Color normalEmission,
            Color faultEmission,
            float minStrength,
            float maxStrength,
            float pulse)
        {
            statusRenderers = renderers != null && renderers.Length > 0
                ? renderers
                : Array.Empty<Renderer>();
            emissionColorProperty = string.IsNullOrWhiteSpace(emissionColorPropertyName) ? "_EmissionColor" : emissionColorPropertyName;
            emissionStrengthProperty = string.IsNullOrWhiteSpace(emissionStrengthPropertyName) ? "_EmissionStrength" : emissionStrengthPropertyName;
            globalQualityProperty = string.IsNullOrWhiteSpace(qualityPropertyName) ? "_H8GlobalQualityWeight" : qualityPropertyName;
            baseEmission = normalEmission;
            failureEmission = faultEmission;
            minEmissionStrength = math.max(0f, minStrength);
            maxEmissionStrength = math.max(minEmissionStrength, maxStrength);
            pulseStrength = math.max(0f, pulse);
            _emissionColorId = 0;
            _emissionStrengthId = 0;
            _globalQualityId = 0;
            _lastQuantizedState = int.MinValue;
            ResolvePropertyIds();
        }
#endif

        private void Awake()
        {
            ResolvePropertyIds();
            // COLD ALLOC: MaterialPropertyBlock[1] - direct renderer status override, no material clones - owner: PowerStatusEmissiveBinding
            _propertyBlock ??= new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _lastQuantizedState = int.MinValue;
        }

        public void ApplyVisualSync(float load01, float failure01, float globalQualityWeight, float normalizedPulsePhase)
        {
            Renderer[] renderers = statusRenderers;
            if (renderers == null || renderers.Length == 0)
                return;

            float quality = math.saturate(math.select(0f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            float load = math.saturate(math.select(0f, load01, math.isfinite(load01)));
            float failure = math.saturate(math.select(0f, failure01, math.isfinite(failure01)));
            float phase = math.saturate(math.select(0f, normalizedPulsePhase, math.isfinite(normalizedPulsePhase)));

            int quantization = quality < 0.05f ? LowTierQualityQuantization : HighTierQualityQuantization;
            int state = (Quantize(load, quantization) * 73856093) ^
                        (Quantize(failure, quantization) * 19349663) ^
                        (Quantize(quality, quantization) * 83492791) ^
                        (quality < 0.05f ? 0 : Quantize(phase, quantization) * 265443576);
            if (_propertyBlock == null || state == _lastQuantizedState)
                return;

            _lastQuantizedState = state;
            ResolvePropertyIds();

            float pulse01 = quality < 0.05f ? 0f : ResolveTrianglePulse01(phase + load * 0.5f);
            float faultBlend = failure;
            Color color = Color.Lerp(baseEmission, failureEmission, faultBlend);
            float strength = math.lerp(minEmissionStrength, maxEmissionStrength, math.saturate(load + failure));
            strength += pulse01 * pulseStrength * quality;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer target = renderers[i];
                if (target == null)
                    continue;

                target.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(_emissionColorId, color);
                _propertyBlock.SetFloat(_emissionStrengthId, strength);
                _propertyBlock.SetFloat(_globalQualityId, quality);
                target.SetPropertyBlock(_propertyBlock);
            }
        }

        private void ResolvePropertyIds()
        {
            if (_emissionColorId == 0)
                _emissionColorId = Shader.PropertyToID(string.IsNullOrEmpty(emissionColorProperty) ? "_EmissionColor" : emissionColorProperty);
            if (_emissionStrengthId == 0)
                _emissionStrengthId = Shader.PropertyToID(string.IsNullOrEmpty(emissionStrengthProperty) ? "_EmissionStrength" : emissionStrengthProperty);
            if (_globalQualityId == 0)
                _globalQualityId = Shader.PropertyToID(string.IsNullOrEmpty(globalQualityProperty) ? "_H8GlobalQualityWeight" : globalQualityProperty);
        }

        private static int Quantize(float value, int steps)
        {
            return (int)math.round(math.saturate(value) * math.max(1, steps));
        }

        private static float ResolveTrianglePulse01(float phase)
        {
            float t = math.frac(phase);
            return 1f - math.abs((t * 2f) - 1f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (statusRenderers == null)
                statusRenderers = Array.Empty<Renderer>();
            if (string.IsNullOrWhiteSpace(emissionColorProperty))
                emissionColorProperty = "_EmissionColor";
            if (string.IsNullOrWhiteSpace(emissionStrengthProperty))
                emissionStrengthProperty = "_EmissionStrength";
            if (string.IsNullOrWhiteSpace(globalQualityProperty))
                globalQualityProperty = "_H8GlobalQualityWeight";
            minEmissionStrength = math.max(0f, minEmissionStrength);
            maxEmissionStrength = math.max(minEmissionStrength, maxEmissionStrength);
            pulseStrength = math.max(0f, pulseStrength);
            _emissionColorId = 0;
            _emissionStrengthId = 0;
            _globalQualityId = 0;
            _lastQuantizedState = int.MinValue;
            ResolvePropertyIds();
        }
#endif
    }
}
