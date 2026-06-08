using Hecton8.Rendering.WaterOptics;
using Hecton8.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Rendering.WaterOptics.Editor
{
    public sealed class AbyssalOpticsTunerWindow : EditorWindow
    {
        private const int PreviewSwatchCount = 64;
        private const int TelemetryBarCount = 64;
        private const float DefaultOceanSurfaceWorldY = 14.02f;
        private readonly VisualElement[] _previewSwatches = new VisualElement[PreviewSwatchCount];
        private readonly VisualElement[] _telemetryBars = new VisualElement[TelemetryBarCount];
        private Slider _absorptionR;
        private Slider _absorptionG;
        private Slider _absorptionB;
        private Slider _extinctionMultiplier;
        private Slider _scatteringR;
        private Slider _scatteringG;
        private Slider _scatteringB;
        private Slider _anisotropy;
        private ColorField _lightColor;
        private Slider _lightIntensity;
        private Slider _surfaceY;
        private Slider _maxDistance;
        private Slider _qualityBias;
        private Toggle _active;
        private Toggle _autoRefreshTelemetry;
        private double _nextTelemetryRefresh;

        [MenuItem("Hecton8/Rendering/Water Optics/Abyssal Optics Tuner")]
        public static void Open()
        {
            GetWindow<AbyssalOpticsTunerWindow>("Abyssal Optics");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _active = new Toggle("Active") { value = true };
            _absorptionR = Slider("Absorption R", 0f, 4f, 0.42f);
            _absorptionG = Slider("Absorption G", 0f, 4f, 0.105f);
            _absorptionB = Slider("Absorption B", 0f, 4f, 0.028f);
            _extinctionMultiplier = Slider("Extinction Multiplier", 0f, 8f, 1f);
            _scatteringR = Slider("Scattering R", 0f, 2f, 0.035f);
            _scatteringG = Slider("Scattering G", 0f, 2f, 0.09f);
            _scatteringB = Slider("Scattering B", 0f, 2f, 0.16f);
            _anisotropy = Slider("Anisotropy", -0.85f, 0.85f, 0.42f);
            _lightColor = new ColorField("Directional Light") { value = new Color(0.09f, 0.42f, 0.70f, 1f), hdr = true };
            _lightIntensity = Slider("Light Intensity", 0f, 8f, 0.85f);
            _surfaceY = Slider("Ocean Surface Y", -2500f, 2500f, DefaultOceanSurfaceWorldY);
            _maxDistance = Slider("Max Travel Meters", 1f, 12000f, 5000f);
            _qualityBias = Slider("Quality Bias", -1f, 1f, 0f);
            _autoRefreshTelemetry = new Toggle("Telemetry Auto Refresh") { value = true };

            root.Add(_active);
            root.Add(_absorptionR);
            root.Add(_absorptionG);
            root.Add(_absorptionB);
            root.Add(_extinctionMultiplier);
            root.Add(_scatteringR);
            root.Add(_scatteringG);
            root.Add(_scatteringB);
            root.Add(_anisotropy);
            root.Add(_lightColor);
            root.Add(_lightIntensity);
            root.Add(_surfaceY);
            root.Add(_maxDistance);
            root.Add(_qualityBias);
            root.Add(_autoRefreshTelemetry);

            Button pullRuntime = new Button(PullRuntimeState) { text = "Pull Runtime DTO" };
            root.Add(pullRuntime);
            Button reloadProfiles = new Button(ReloadCsvProfiles) { text = "Reload CSV Profiles" };
            root.Add(reloadProfiles);

            VisualElement preview = new VisualElement();
            preview.style.flexDirection = FlexDirection.Row;
            preview.style.height = 34;
            preview.style.marginTop = 8;
            preview.style.borderTopWidth = 1;
            preview.style.borderBottomWidth = 1;
            preview.style.borderLeftWidth = 1;
            preview.style.borderRightWidth = 1;
            preview.style.borderTopColor = new Color(0.09f, 0.13f, 0.16f);
            preview.style.borderBottomColor = new Color(0.09f, 0.13f, 0.16f);
            preview.style.borderLeftColor = new Color(0.09f, 0.13f, 0.16f);
            preview.style.borderRightColor = new Color(0.09f, 0.13f, 0.16f);
            root.Add(preview);

            for (int i = 0; i < PreviewSwatchCount; i++)
            {
                VisualElement swatch = new VisualElement();
                swatch.style.flexGrow = 1f;
                preview.Add(swatch);
                _previewSwatches[i] = swatch;
            }

            VisualElement telemetry = new VisualElement();
            telemetry.style.flexDirection = FlexDirection.Row;
            telemetry.style.alignItems = Align.FlexEnd;
            telemetry.style.height = 42;
            telemetry.style.marginTop = 8;
            telemetry.style.borderTopWidth = 1;
            telemetry.style.borderBottomWidth = 1;
            telemetry.style.borderLeftWidth = 1;
            telemetry.style.borderRightWidth = 1;
            telemetry.style.borderTopColor = new Color(0.10f, 0.16f, 0.18f);
            telemetry.style.borderBottomColor = new Color(0.10f, 0.16f, 0.18f);
            telemetry.style.borderLeftColor = new Color(0.10f, 0.16f, 0.18f);
            telemetry.style.borderRightColor = new Color(0.10f, 0.16f, 0.18f);
            root.Add(telemetry);

            for (int i = 0; i < TelemetryBarCount; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.flexGrow = 1f;
                bar.style.height = 1f;
                bar.style.marginLeft = 1f;
                bar.style.backgroundColor = new Color(0.05f, 0.38f, 0.74f, 1f);
                telemetry.Add(bar);
                _telemetryBars[i] = bar;
            }

            RegisterCallbacks();
            PullRuntimeState();
            ApplyToRuntimeAndPreview();
            UpdateTelemetryGraph();
        }

        private void Update()
        {
            if (_autoRefreshTelemetry == null || !_autoRefreshTelemetry.value)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextTelemetryRefresh)
                return;

            _nextTelemetryRefresh = now + 0.25d;
            UpdateTelemetryGraph();
        }

        private void RegisterCallbacks()
        {
            _active.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
            _absorptionR.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
            _absorptionG.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
            _absorptionB.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
            _extinctionMultiplier.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
            _scatteringR.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
            _scatteringG.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
            _scatteringB.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
            _anisotropy.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
            _lightColor.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
            _lightIntensity.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
            _surfaceY.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
            _maxDistance.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
            _qualityBias.RegisterValueChangedCallback(_ => ApplyToRuntimeAndPreview());
        }

        private void PullRuntimeState()
        {
            if (!Application.isPlaying ||
                !WaterOpticsRuntime.TryGetRuntimeInstance(out WaterOpticsRuntime runtime) ||
                !runtime.TryReadLatestParams(out WaterOpticsDTO dto))
            {
                return;
            }

            _absorptionR.SetValueWithoutNotify(dto.AbsorptionCoefficientsRGB.x);
            _absorptionG.SetValueWithoutNotify(dto.AbsorptionCoefficientsRGB.y);
            _absorptionB.SetValueWithoutNotify(dto.AbsorptionCoefficientsRGB.z);
            _extinctionMultiplier.SetValueWithoutNotify(dto.AbsorptionCoefficientsRGB.w);
            _scatteringR.SetValueWithoutNotify(dto.ScatteringCoefficientsRGB.x);
            _scatteringG.SetValueWithoutNotify(dto.ScatteringCoefficientsRGB.y);
            _scatteringB.SetValueWithoutNotify(dto.ScatteringCoefficientsRGB.z);
            _anisotropy.SetValueWithoutNotify(dto.ScatteringCoefficientsRGB.w);
            _lightColor.SetValueWithoutNotify(new Color(
                dto.DirectionalLightColorAndIntensity.x,
                dto.DirectionalLightColorAndIntensity.y,
                dto.DirectionalLightColorAndIntensity.z,
                1f));
            _lightIntensity.SetValueWithoutNotify(dto.DirectionalLightColorAndIntensity.w);
            _maxDistance.SetValueWithoutNotify(Mathf.Max(1f, dto.QualityAndDepthLimits.z));
            _surfaceY.SetValueWithoutNotify(dto.QualityAndDepthLimits.y);
            _active.SetValueWithoutNotify(dto.QualityAndDepthLimits.w > 0.5f);
            if (runtime.TryReadLatestTuning(out WaterOpticsTuningDTO tuning))
                _qualityBias.SetValueWithoutNotify(Mathf.Clamp(tuning.MaxDistanceQualityFlagsProfile.y, -1f, 1f));
            UpdateTelemetryGraph();
        }

        private void ReloadCsvProfiles()
        {
            if (!Application.isPlaying ||
                !WaterOpticsRuntime.TryGetRuntimeInstance(out WaterOpticsRuntime runtime) ||
                !runtime.TryReloadEditorProfilesCsv())
            {
                return;
            }

            PullRuntimeState();
        }

        private void ApplyToRuntimeAndPreview()
        {
            Vector4 absorption = new Vector4(_absorptionR.value, _absorptionG.value, _absorptionB.value, _extinctionMultiplier.value);
            Vector4 scattering = new Vector4(_scatteringR.value, _scatteringG.value, _scatteringB.value, _anisotropy.value);
            Color light = _lightColor.value;
            Vector4 lightAndIntensity = new Vector4(light.r, light.g, light.b, _lightIntensity.value);

            if (Application.isPlaying && WaterOpticsRuntime.TryGetRuntimeInstance(out WaterOpticsRuntime runtime))
                runtime.ApplyEditorTuning(absorption, scattering, lightAndIntensity, _surfaceY.value, _maxDistance.value, _qualityBias.value, _active.value);

            UpdatePreview(absorption, scattering);
        }

        private void UpdatePreview(Vector4 absorptionAndMultiplier, Vector4 scatteringAndAnisotropy)
        {
            Vector3 absorption = new Vector3(absorptionAndMultiplier.x, absorptionAndMultiplier.y, absorptionAndMultiplier.z) * Mathf.Max(0f, absorptionAndMultiplier.w);
            Vector3 scattering = new Vector3(scatteringAndAnisotropy.x, scatteringAndAnisotropy.y, scatteringAndAnisotropy.z);
            Vector3 extinction = absorption + scattering;
            float maxDistance = Mathf.Max(1f, _maxDistance.value);

            for (int i = 0; i < PreviewSwatchCount; i++)
            {
                float t = PreviewSwatchCount <= 1 ? 0f : i / (float)(PreviewSwatchCount - 1);
                float distance = maxDistance * t;
                Color color = new Color(
                    MathLodApproximation.ApproxExpNegPade33Wide40(distance * extinction.x),
                    MathLodApproximation.ApproxExpNegPade33Wide40(distance * extinction.y),
                    MathLodApproximation.ApproxExpNegPade33Wide40(distance * extinction.z),
                    1f);
                _previewSwatches[i].style.backgroundColor = color;
            }
        }

        private void UpdateTelemetryGraph()
        {
            if (_telemetryBars[0] == null)
                return;

            if (!Application.isPlaying ||
                !WaterOpticsRuntime.TryGetRuntimeInstance(out WaterOpticsRuntime runtime))
            {
                ClearTelemetryGraph();
                return;
            }

            for (int i = 0; i < TelemetryBarCount; i++)
            {
                int framesBack = TelemetryBarCount - 1 - i;
                if (!runtime.TryReadTelemetryEntry(framesBack, out WaterOpticsTelemetryEntry entry))
                {
                    _telemetryBars[i].style.height = 1f;
                    _telemetryBars[i].style.backgroundColor = new Color(0.04f, 0.06f, 0.07f, 1f);
                    continue;
                }

                float gpu01 = Mathf.Clamp01(entry.EstimatedOpaqueGpuMicroseconds / 20f);
                float spectral = Mathf.Clamp01(entry.ActiveSpectralWeight);
                float height = 1f + gpu01 * 40f;
                _telemetryBars[i].style.height = height;
                _telemetryBars[i].style.backgroundColor = new Color(
                    Mathf.Lerp(0.04f, 0.90f, spectral),
                    Mathf.Lerp(0.26f, 0.62f, gpu01),
                    Mathf.Lerp(0.42f, 0.95f, 1f - spectral * 0.35f),
                    1f);
            }
        }

        private void ClearTelemetryGraph()
        {
            if (_telemetryBars[0] == null)
                return;

            for (int i = 0; i < TelemetryBarCount; i++)
            {
                _telemetryBars[i].style.height = 1f;
                _telemetryBars[i].style.backgroundColor = new Color(0.04f, 0.06f, 0.07f, 1f);
            }
        }

        private static Slider Slider(string label, float low, float high, float value)
        {
            Slider slider = new Slider(label, low, high)
            {
                value = value,
                showInputField = true
            };
            return slider;
        }
    }
}
