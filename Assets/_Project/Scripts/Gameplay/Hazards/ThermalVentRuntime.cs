using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Hazards/Thermal Vent Runtime")]
    public sealed class ThermalVentRuntime : MonoBehaviour, ILateFrameTickable
    {
        private const float PropertyWriteEpsilon = 0.0001f;
        private const float LightQualityCutoff = 0.02f;
        private const float DecalQualityCutoff = 0.04f;

        [SerializeField] private HazardMetadata metadata;
        [SerializeField] private Light keyLight;
        [SerializeField] private DecalProjector primaryDecal;
        [SerializeField] private LightCullingProxy cullingProxy;
        [SerializeField] private float baseLightIntensity = 2.5f;
        [SerializeField] private float baseLightRange = 12f;
        [SerializeField] private float baseDecalFade = 0.7f;
        [SerializeField] private float pulseFrequencyHz = 0.45f;
        [SerializeField] private float pulseAmplitude = 0.18f;

        private bool _lateFrameRegistered;
        private int _presentationPhase;
        private float _lastAppliedLightIntensity = -1f;
        private float _lastAppliedLightRange = -1f;
        private float _lastAppliedDecalFade = -1f;

        public HazardMetadata Metadata => metadata;
        public Light KeyLight => keyLight;
        public DecalProjector PrimaryDecal => primaryDecal;
        public LightCullingProxy CullingProxy => cullingProxy;
        public bool HasValidFactoryConfiguration => metadata != null && keyLight != null && primaryDecal != null && cullingProxy != null;

        public void ConfigureForEditor(
            HazardMetadata newMetadata,
            Light newKeyLight,
            DecalProjector newPrimaryDecal,
            LightCullingProxy newCullingProxy,
            float newBaseLightIntensity,
            float newBaseLightRange,
            float newBaseDecalFade,
            float newPulseFrequencyHz,
            float newPulseAmplitude)
        {
            metadata = newMetadata;
            keyLight = newKeyLight;
            primaryDecal = newPrimaryDecal;
            cullingProxy = newCullingProxy;
            baseLightIntensity = Mathf.Max(0f, SanitizeFinite(newBaseLightIntensity, 2.5f));
            baseLightRange = Mathf.Max(0.5f, SanitizeFinite(newBaseLightRange, 12f));
            baseDecalFade = Mathf.Clamp01(SanitizeFinite(newBaseDecalFade, 0.7f));
            pulseFrequencyHz = Mathf.Clamp(SanitizeFinite(newPulseFrequencyHz, 0.45f), 0f, 4f);
            pulseAmplitude = Mathf.Clamp01(SanitizeFinite(newPulseAmplitude, 0.18f));
            ResetPresentationWriteCache();
            EnforceLightPolicy();
        }

        private void Awake()
        {
            if (metadata == null)
                TryGetComponent(out metadata);

            _presentationPhase = ResolvePresentationPhase(metadata);
            EnforceLightPolicy();
        }

        private void OnEnable()
        {
            EnforceLightPolicy();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
        }

        private void OnDestroy()
        {
            TryUnregisterLateFrame();
        }

        public void LateFrameTick()
        {
            Light light = keyLight;
            DecalProjector decal = primaryDecal;
            if (light == null && decal == null)
            {
                ResetPresentationWriteCache();
                TryUnregisterLateFrame();
                return;
            }

            bool hasExternalCulling = cullingProxy != null;
            if (hasExternalCulling &&
                (light == null || !light.enabled) &&
                (decal == null || !decal.enabled))
            {
                ResetPresentationWriteCache();
                return;
            }

            float quality = Sanitize01(HomeostasisBrain.GlobalQualityWeight);
            float curve = quality * quality * (3f - (2f * quality));

            if (curve <= LightQualityCutoff)
            {
                if (light != null && light.enabled)
                    light.enabled = false;
                if (decal != null && curve <= DecalQualityCutoff && decal.enabled)
                    decal.enabled = false;
                ResetPresentationWriteCache();
                return;
            }

            int stride = ResolvePresentationFrameStride(curve);
            bool needsPresentationWrite = HasDirtyPresentationWriteCache(light, decal);
            if (stride > 1 &&
                !needsPresentationWrite &&
                ((Time.frameCount + _presentationPhase) % stride) != 0 &&
                (light == null || light.enabled) &&
                (decal == null || decal.enabled || curve <= DecalQualityCutoff))
            {
                return;
            }

            if (light != null)
            {
                if (light.shadows != LightShadows.None)
                    light.shadows = LightShadows.None;

                if (!hasExternalCulling && !light.enabled)
                    light.enabled = true;

                if (light.enabled)
                {
                    float pulse = ResolvePulse(curve);
                    float targetIntensity = baseLightIntensity * curve * pulse;
                    float targetRange = Mathf.Max(0.5f, baseLightRange * Mathf.Lerp(0.7f, 1.2f, curve));
                    if (ShouldApplyProperty(_lastAppliedLightIntensity, targetIntensity))
                    {
                        light.intensity = targetIntensity;
                        _lastAppliedLightIntensity = targetIntensity;
                    }
                    if (ShouldApplyProperty(_lastAppliedLightRange, targetRange))
                    {
                        light.range = targetRange;
                        _lastAppliedLightRange = targetRange;
                    }
                }
            }

            if (decal != null)
            {
                if (curve > DecalQualityCutoff)
                {
                    if (!hasExternalCulling && !decal.enabled)
                        decal.enabled = true;
                    if (decal.enabled)
                    {
                        float targetFade = baseDecalFade * curve;
                        if (ShouldApplyProperty(_lastAppliedDecalFade, targetFade))
                        {
                            decal.fadeFactor = targetFade;
                            _lastAppliedDecalFade = targetFade;
                        }
                    }
                }
                else if (decal.enabled)
                {
                    decal.enabled = false;
                    _lastAppliedDecalFade = -1f;
                }
            }
        }

        private float ResolvePulse(float curve)
        {
            if (pulseFrequencyHz <= 0f || pulseAmplitude <= 0f)
                return 1f;

            float phase = Mathf.Repeat(Time.unscaledTime * pulseFrequencyHz, 1f);
            float triangle = 1f - Mathf.Abs((phase * 2f) - 1f);
            return 1f + (((triangle * 2f) - 1f) * pulseAmplitude * curve);
        }

        private void EnforceLightPolicy()
        {
            if (keyLight != null)
                keyLight.shadows = LightShadows.None;
        }

        private void TryRegisterLateFrame()
        {
            if (_lateFrameRegistered || !Application.isPlaying || !HasPresentationTarget())
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameRegistered = false;
        }

        private void OnValidate()
        {
            baseLightIntensity = Mathf.Max(0f, SanitizeFinite(baseLightIntensity, 2.5f));
            baseLightRange = Mathf.Max(0.5f, SanitizeFinite(baseLightRange, 12f));
            baseDecalFade = Mathf.Clamp01(SanitizeFinite(baseDecalFade, 0.7f));
            pulseFrequencyHz = Mathf.Clamp(SanitizeFinite(pulseFrequencyHz, 0.45f), 0f, 4f);
            pulseAmplitude = Mathf.Clamp01(SanitizeFinite(pulseAmplitude, 0.18f));
            ResetPresentationWriteCache();
            EnforceLightPolicy();
        }

        private bool HasPresentationTarget()
        {
            return keyLight != null || primaryDecal != null;
        }

        private static int ResolvePresentationPhase(HazardMetadata sourceMetadata)
        {
            return sourceMetadata != null ? (int)(sourceMetadata.HazardHash & 3u) : 0;
        }

        private static int ResolvePresentationFrameStride(float curve)
        {
            return Mathf.Clamp(1 + (int)((1f - Mathf.Clamp01(curve)) * 4f), 1, 5);
        }

        private void ResetPresentationWriteCache()
        {
            _lastAppliedLightIntensity = -1f;
            _lastAppliedLightRange = -1f;
            _lastAppliedDecalFade = -1f;
        }

        private bool HasDirtyPresentationWriteCache(Light light, DecalProjector decal)
        {
            return (light != null &&
                    light.enabled &&
                    (_lastAppliedLightIntensity < 0f || _lastAppliedLightRange < 0f)) ||
                   (decal != null &&
                    decal.enabled &&
                    _lastAppliedDecalFade < 0f);
        }

        private static bool ShouldApplyProperty(float previous, float next)
        {
            return !IsFinite(previous) || Mathf.Abs(previous - next) > PropertyWriteEpsilon;
        }

        private static float Sanitize01(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
