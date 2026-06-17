#if UNITY_EDITOR
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Atmosphere.Editor
{
    public sealed class ShinobuAtmosphereWaveTunerWindow : EditorWindow
    {
        private float _windSpeed = 11f;
        private float _waveSteepness = 0.52f;
        private float _gasGiantGlow = 1.15f;
        private float _foamThreshold = 0.72f;
        private float _qualityMin = 0f;
        private float _qualityMax = 1f;
        private bool _drawGizmo = true;
        private Slider _windSpeedSlider;
        private Slider _waveSteepnessSlider;
        private Slider _gasGiantGlowSlider;
        private Slider _foamThresholdSlider;
        private Slider _qualityMinSlider;
        private Slider _qualityMaxSlider;
        private Toggle _drawGizmoToggle;
        private Label _statusLabel;
        private bool _suppressUiCallbacks;

        [MenuItem("Hecton8/Atmosphere & Wave Tuner")]
        public static void Open()
        {
            ShinobuAtmosphereWaveTunerWindow window = GetWindow<ShinobuAtmosphereWaveTunerWindow>();
            window.titleContent = new GUIContent("Atmosphere & Wave Tuner");
            window.minSize = new Vector2(360f, 220f);
            window.RefreshFromVault();
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
            RefreshFromVault();
            PushValuesToControls();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _windSpeedSlider = CreateSlider("Wind Speed", 0f, 40f);
            _waveSteepnessSlider = CreateSlider("Wave Steepness (Choppiness)", 0f, 1f);
            _gasGiantGlowSlider = CreateSlider("Gas Giant Glow Intensity", 0f, 4f);
            _foamThresholdSlider = CreateSlider("Foam Threshold", 0f, 1f);
            _qualityMinSlider = CreateSlider("Quality Step Min", 0f, 1f);
            _qualityMaxSlider = CreateSlider("Quality Step Max", 0f, 1f);
            _drawGizmoToggle = new Toggle("Scene Wave Grid");
            _drawGizmoToggle.RegisterValueChangedCallback(evt =>
            {
                if (_suppressUiCallbacks)
                    return;

                _drawGizmo = evt.newValue;
                SceneView.RepaintAll();
            });

            _statusLabel = new Label();
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.marginTop = 6f;

            root.Add(_windSpeedSlider);
            root.Add(_waveSteepnessSlider);
            root.Add(_gasGiantGlowSlider);
            root.Add(_foamThresholdSlider);
            root.Add(_qualityMinSlider);
            root.Add(_qualityMaxSlider);
            root.Add(_drawGizmoToggle);
            root.Add(_statusLabel);

            RefreshFromVault();
            PushValuesToControls();
            UpdateStatusLabel();
        }

        private void OnFocus()
        {
            RefreshFromVault();
            PushValuesToControls();
            UpdateStatusLabel();
        }

        private Slider CreateSlider(string label, float lowValue, float highValue)
        {
            Slider slider = new Slider(label, lowValue, highValue);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(_ =>
            {
                if (_suppressUiCallbacks)
                    return;

                PullValuesFromControls();
                ShinobuOceanSurfaceAtmosphereRuntime.TryApplyTunerValues(_windSpeed, _waveSteepness, _gasGiantGlow, _foamThreshold, _qualityMin, _qualityMax);
                SceneView.RepaintAll();
                UpdateStatusLabel();
            });

            return slider;
        }

        private void PushValuesToControls()
        {
            if (_windSpeedSlider == null)
                return;

            _suppressUiCallbacks = true;
            _windSpeedSlider.SetValueWithoutNotify(_windSpeed);
            _waveSteepnessSlider.SetValueWithoutNotify(_waveSteepness);
            _gasGiantGlowSlider.SetValueWithoutNotify(_gasGiantGlow);
            _foamThresholdSlider.SetValueWithoutNotify(_foamThreshold);
            _qualityMinSlider.SetValueWithoutNotify(_qualityMin);
            _qualityMaxSlider.SetValueWithoutNotify(_qualityMax);
            _drawGizmoToggle.SetValueWithoutNotify(_drawGizmo);
            _suppressUiCallbacks = false;
        }

        private void PullValuesFromControls()
        {
            _windSpeed = _windSpeedSlider.value;
            _waveSteepness = _waveSteepnessSlider.value;
            _gasGiantGlow = _gasGiantGlowSlider.value;
            _foamThreshold = _foamThresholdSlider.value;
            _qualityMin = _qualityMinSlider.value;
            _qualityMax = _qualityMaxSlider.value;
        }

        private void UpdateStatusLabel()
        {
            if (_statusLabel == null)
                return;

            _statusLabel.text = ShinobuOceanSurfaceAtmosphereRuntime.TryGetVaultSnapshot(out _, out _, out _)
                ? ResolveReadbackStatusText()
                : "GlobalDataVault ocean buffers are not active. Enter play mode or enable ShinobuOceanSurfaceAtmosphereRuntime.";
        }

        private static string ResolveReadbackStatusText()
        {
            if (ShinobuOceanSurfaceAtmosphereRuntime.IsWaveParameterMutationLocked)
                return "GlobalDataVault ocean buffers active. Wave parameter job owns the mutation lease; retry next frame.";

            if (!ShinobuOceanSurfaceAtmosphereRuntime.TryGetTelemetrySnapshot(
                    out NativeArray<OceanSurfaceTelemetryEntry>.ReadOnly telemetry) ||
                telemetry.Length <= 0)
            {
                return "GlobalDataVault ocean buffers active. Surface telemetry pending.";
            }

            OceanSurfaceTelemetryEntry latest = ResolveLatestTelemetry(telemetry);
            return "GlobalDataVault ocean buffers active. Surface telemetry latency " +
                   latest.ReadbackLatencyFrames +
                   " frames, samples " +
                   latest.ReadbackSampleCount +
                   ", flags " +
                   ResolveTelemetryFlagStatusText(latest.Flags) +
                   ".";
        }

        private static string ResolveTelemetryFlagStatusText(uint flags)
        {
            if (flags == 0u)
                return "ok";

            string text = string.Empty;
            AppendTelemetryFlag(ref text, flags, OceanSurfaceAtmosphereConstants.TelemetryFlagReadbackOrComputeBudget, "readback-budget");
            AppendTelemetryFlag(ref text, flags, OceanSurfaceAtmosphereConstants.TelemetryFlagEmergencyWeatherFallback, "emergency-weather");
            AppendTelemetryFlag(ref text, flags, OceanSurfaceAtmosphereConstants.TelemetryFlagDataVaultRehydrated, "data-vault-rehydrated");
            AppendTelemetryFlag(ref text, flags, OceanSurfaceAtmosphereConstants.TelemetryFlagMissingRuntimeData, "missing-runtime-data");
            return text.Length == 0 ? "unknown" : text;
        }

        private static void AppendTelemetryFlag(ref string text, uint flags, uint flag, string label)
        {
            if ((flags & flag) == 0u)
                return;

            text = text.Length == 0 ? label : text + "," + label;
        }

        private void RefreshFromVault()
        {
            if (ShinobuOceanSurfaceAtmosphereRuntime.IsWaveParameterMutationLocked)
                return;

            if (!ShinobuOceanSurfaceAtmosphereRuntime.TryGetVaultSnapshot(
                    out NativeArray<WaveParametersDTO>.ReadOnly waves,
                    out NativeArray<WeatherStateDTO>.ReadOnly weather,
                    out NativeArray<AtmosphereDTO>.ReadOnly atmosphere))
            {
                return;
            }

            if (weather.Length > 0)
            {
                WeatherStateDTO state = weather[0];
                _windSpeed = state.WindDirectionSpeedStorm.z;
                _foamThreshold = state.SurfaceScalars.z;
            }

            if (waves.Length > 0)
                _waveSteepness = HectonOceanSurfaceMath.WaveLaneSteepness(waves[0].Wave1);

            if (atmosphere.Length > 0)
                _gasGiantGlow = atmosphere[0].ScatteringParams.y;
        }

        private static OceanSurfaceTelemetryEntry ResolveLatestTelemetry(NativeArray<OceanSurfaceTelemetryEntry>.ReadOnly telemetry)
        {
            OceanSurfaceTelemetryEntry latest = default;
            if (telemetry.Length <= 0)
                return latest;

            for (int i = 0; i < telemetry.Length; i++)
            {
                if (telemetry[i].Frame >= latest.Frame)
                    latest = telemetry[i];
            }

            return latest;
        }

        private void DrawSceneGizmos(SceneView sceneView)
        {
            if (!_drawGizmo ||
                !ShinobuOceanSurfaceAtmosphereRuntime.TryGetVaultSnapshot(
                    out NativeArray<WaveParametersDTO>.ReadOnly waves,
                    out NativeArray<WeatherStateDTO>.ReadOnly weather,
                    out _))
            {
                return;
            }

            Camera camera = sceneView.camera;
            if (camera == null || waves.Length == 0 || weather.Length == 0)
                return;

            WeatherStateDTO state = weather[0];
            Handles.color = new Color(0.1f, 0.45f, 1f, 0.7f);
            float baseY = state.SurfaceScalars.x;
            if (ShinobuOceanSurfaceAtmosphereRuntime.TryGetReadbackDebugSnapshot(
                    out NativeArray<float4>.ReadOnly queries,
                    out NativeArray<float4>.ReadOnly results,
                    out NativeArray<OceanSurfaceTelemetryEntry>.ReadOnly telemetry) &&
                queries.Length > 0 &&
                results.Length > 0)
            {
                int sampleCount = math.min(ResolveLatestTelemetry(telemetry).ReadbackSampleCount, math.min(queries.Length, results.Length));
                for (int i = 0; i < sampleCount; i++)
                {
                    float4 query = queries[i];
                    float4 result = results[i];
                    Vector3 point = new Vector3(query.z, baseY + result.x, query.w);
                    Handles.SphereHandleCap(0, point, Quaternion.identity, 0.35f, EventType.Repaint);
                    Handles.DrawLine(new Vector3(query.z, baseY - 2f, query.w), point);
                }

                return;
            }

        }
    }
}
#endif
