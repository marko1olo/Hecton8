#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
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
        private bool _drawGizmo = true;
        private Slider _windSpeedSlider;
        private Slider _waveSteepnessSlider;
        private Slider _gasGiantGlowSlider;
        private Slider _foamThresholdSlider;
        private Toggle _drawGizmoToggle;
        private Label _statusLabel;
        private bool _suppressUiCallbacks;

        [MenuItem("HECTON-8/Atmosphere & Wave Tuner")]
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
            SceneView.duringSceneGui += OnDrawGizmos;
            RefreshFromVault();
            PushValuesToControls();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnDrawGizmos;
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
                ShinobuOceanSurfaceAtmosphereRuntime.TryApplyTunerValues(_windSpeed, _waveSteepness, _gasGiantGlow, _foamThreshold);
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
            _drawGizmoToggle.SetValueWithoutNotify(_drawGizmo);
            _suppressUiCallbacks = false;
        }

        private void PullValuesFromControls()
        {
            _windSpeed = _windSpeedSlider.value;
            _waveSteepness = _waveSteepnessSlider.value;
            _gasGiantGlow = _gasGiantGlowSlider.value;
            _foamThreshold = _foamThresholdSlider.value;
        }

        private void UpdateStatusLabel()
        {
            if (_statusLabel == null)
                return;

            _statusLabel.text = ShinobuOceanSurfaceAtmosphereRuntime.TryGetVaultSnapshot(out _, out _, out _)
                ? "GlobalDataVault ocean buffers active."
                : "GlobalDataVault ocean buffers are not active. Enter play mode or enable ShinobuOceanSurfaceAtmosphereRuntime.";
        }

        private void RefreshFromVault()
        {
            if (!ShinobuOceanSurfaceAtmosphereRuntime.TryGetVaultSnapshot(
                    out NativeArray<WaveParametersDTO> waves,
                    out NativeArray<WeatherStateDTO> weather,
                    out NativeArray<AtmosphereDTO> atmosphere))
            {
                return;
            }

            if (weather.IsCreated && weather.Length > 0)
            {
                WeatherStateDTO state = weather[0];
                _windSpeed = state.WindDirectionSpeedStorm.z;
                _foamThreshold = state.SurfaceScalars.z;
            }

            if (waves.IsCreated && waves.Length > 0)
                _waveSteepness = waves[0].DirectionAndSteepness.w;

            if (atmosphere.IsCreated && atmosphere.Length > 0)
                _gasGiantGlow = atmosphere[0].ScatteringParams.y;
        }

        private void OnDrawGizmos(SceneView sceneView)
        {
            if (!_drawGizmo ||
                !ShinobuOceanSurfaceAtmosphereRuntime.TryGetVaultSnapshot(
                    out NativeArray<WaveParametersDTO> waves,
                    out NativeArray<WeatherStateDTO> weather,
                    out _))
            {
                return;
            }

            Camera camera = sceneView.camera;
            if (camera == null || !waves.IsCreated || waves.Length == 0 || !weather.IsCreated || weather.Length == 0)
                return;

            WeatherStateDTO state = weather[0];
            double3 originAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(camera.transform.position);
            float quality = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            if (quality <= 0f)
                quality = 1f;

            Handles.color = new Color(0.1f, 0.45f, 1f, 0.7f);
            const int halfGrid = 5;
            const float spacing = 12f;
            float baseY = state.SurfaceScalars.x;
            for (int z = -halfGrid; z <= halfGrid; z++)
            {
                for (int x = -halfGrid; x <= halfGrid; x++)
                {
                    Vector3 runtime = camera.transform.position + new Vector3(x * spacing, 0f, z * spacing);
                    double3 aup = originAup + new double3(x * spacing, 0.0, z * spacing);
                    HectonOceanSurfaceMath.EvaluateWaves(aup, (float)EditorApplication.timeSinceStartup, waves, quality, out float relativeHeight, out _);
                    float surfaceY = baseY + relativeHeight;
                    Vector3 top = new Vector3(runtime.x, surfaceY, runtime.z);
                    Vector3 bottom = new Vector3(runtime.x, baseY - 3f, runtime.z);
                    Handles.DrawLine(bottom, top);
                }
            }
        }
    }
}
#endif
