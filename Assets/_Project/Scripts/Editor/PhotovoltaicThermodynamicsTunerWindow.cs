#if UNITY_EDITOR
using Hecton8.Power;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class PhotovoltaicThermodynamicsTunerWindow : EditorWindow
    {
        private readonly SolarTelemetryEntry[] _telemetry = new SolarTelemetryEntry[SolarPowerGenerationConstants.TelemetryFrameCount];
        private Slider _attenuation;
        private Slider _turbidity;
        private Slider _turbidityGain;
        private Slider _irradiance;
        private Slider _baseEfficiency;
        private Label _summary;
        private IMGUIContainer _graph;
        private IVisualElementScheduledItem _tickSchedule;
        private int _telemetryCount;
        private uint _lastSummaryFrameIndex;
        private uint _lastSummaryStateHash;
        private uint _lastSummarySolverMicroseconds;
        private int _lastSummaryPanelCount = -1;
        private bool _summaryShowsUnavailable;
        private bool _summaryShowsEmpty = true;

        [MenuItem("Hecton8/Power/Photovoltaic Thermodynamics Tuner")]
        public static void Open()
        {
            GetWindow<PhotovoltaicThermodynamicsTunerWindow>("PV Thermodynamics");
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _summary = new Label("No solar telemetry.");
            root.Add(_summary);

            _attenuation = AddSlider(root, "Water Attenuation", 0.0001f, 0.25f, SolarPowerGenerationConstants.DefaultWaterAttenuationCoefficient);
            _turbidity = AddSlider(root, "Water Turbidity", 0f, 4f, 1f);
            _turbidityGain = AddSlider(root, "Turbidity Gain", 0f, 2f, SolarPowerGenerationConstants.DefaultTurbidityMultiplier);
            _irradiance = AddSlider(root, "Solar Irradiance W", 10f, 2000f, SolarPowerGenerationConstants.DefaultSolarIrradianceWatts);
            _baseEfficiency = AddSlider(root, "Base Efficiency Scalar", 0f, 4f, 0f);

            _graph = new IMGUIContainer(DrawGraph);
            _graph.style.height = 180;
            _graph.style.marginTop = 8;
            root.Add(_graph);

            RegisterSlider(_attenuation);
            RegisterSlider(_turbidity);
            RegisterSlider(_turbidityGain);
            RegisterSlider(_irradiance);
            RegisterSlider(_baseEfficiency);
            _tickSchedule = root.schedule.Execute(EditorTick).Every(200);
        }

        private void OnDisable()
        {
            _tickSchedule?.Pause();
            _tickSchedule = null;
        }

        private Slider AddSlider(VisualElement root, string label, float low, float high, float value)
        {
            Slider slider = new Slider(label, low, high)
            {
                value = value,
                showInputField = true
            };
            root.Add(slider);
            return slider;
        }

        private void RegisterSlider(Slider slider)
        {
            slider.RegisterValueChangedCallback(OnSliderValueChanged);
        }

        private void OnSliderValueChanged(ChangeEvent<float> evt)
        {
            PushTuning();
        }

        private void PushTuning()
        {
            SolarConditionsDTO tuning = default;
            SolarPowerGenerationRuntime.TryGetTuning(out tuning);
            tuning.WaterAttenuationCoefficient = math.max(0.0001f, _attenuation.value);
            tuning.WaterTurbidity = math.max(0f, _turbidity.value);
            tuning.TurbidityMultiplier = math.max(0f, _turbidityGain.value);
            tuning.InitialIntensityWatts = math.max(10f, _irradiance.value);
            tuning.BaseEfficiencyScalar = math.max(0f, _baseEfficiency.value);
            SolarPowerGenerationRuntime.SetTuning(in tuning);
        }

        private void EditorTick()
        {
            if (!SolarPowerGenerationRuntime.TryCopyTelemetry(_telemetry, out _telemetryCount))
            {
                if (!_summaryShowsUnavailable)
                {
                    _summary.text = "Play Mode solar telemetry unavailable.";
                    _summaryShowsUnavailable = true;
                    _summaryShowsEmpty = false;
                    _graph.MarkDirtyRepaint();
                }

                return;
            }

            _summaryShowsUnavailable = false;
            if (_telemetryCount > 0)
            {
                _summaryShowsEmpty = false;
                SolarTelemetryEntry latest = _telemetry[0];
                if (latest.FrameIndex != _lastSummaryFrameIndex ||
                    latest.StateHash != _lastSummaryStateHash ||
                    latest.SolverMicroseconds != _lastSummarySolverMicroseconds ||
                    latest.ActivePanelCount != _lastSummaryPanelCount)
                {
                    _summary.text =
                        $"Panels {latest.ActivePanelCount} | Total {latest.TotalGeneratedWatts:F1} W | Peak {latest.PeakPanelWatts:F1} W | Depth {latest.AverageDepthMeters:F1} m | Beer {latest.AverageOpticalDepth:F2} | {latest.SolverMicroseconds} us";
                    _lastSummaryFrameIndex = latest.FrameIndex;
                    _lastSummaryStateHash = latest.StateHash;
                    _lastSummarySolverMicroseconds = latest.SolverMicroseconds;
                    _lastSummaryPanelCount = latest.ActivePanelCount;
                    _graph.MarkDirtyRepaint();
                }

                return;
            }

            if (!_summaryShowsEmpty)
            {
                _summary.text = "No solar telemetry.";
                _summaryShowsEmpty = true;
                _graph.MarkDirtyRepaint();
            }
        }

        private void DrawGraph()
        {
            Rect rect = GUILayoutUtility.GetRect(position.width - 24f, 160f);
            EditorGUI.DrawRect(rect, new Color(0.05f, 0.055f, 0.06f));
            if (_telemetryCount <= 1)
                return;

            float peak = 1f;
            for (int i = 0; i < _telemetryCount; i++)
                peak = math.max(peak, _telemetry[i].TotalGeneratedWatts);

            Handles.BeginGUI();
            Handles.color = new Color(1f, 0.82f, 0.18f, 1f);
            Vector3 previous = Vector3.zero;
            for (int i = 0; i < _telemetryCount; i++)
            {
                float x = rect.xMin + rect.width * (1f - i * math.rcp(math.max(1, _telemetryCount - 1)));
                float y = rect.yMax - rect.height * math.saturate(_telemetry[i].TotalGeneratedWatts / peak);
                Vector3 point = new Vector3(x, y, 0f);
                if (i > 0)
                    Handles.DrawAAPolyLine(2f, previous, point);
                previous = point;
            }
            Handles.color = new Color(0.2f, 0.75f, 1f, 0.85f);
            for (int i = 0; i < _telemetryCount; i += math.max(1, _telemetryCount / 32))
            {
                float x = rect.xMin + rect.width * (1f - i * math.rcp(math.max(1, _telemetryCount - 1)));
                float shadow = _telemetry[i].ActivePanelCount > 0
                    ? _telemetry[i].ShadowedPanelCount * math.rcp(_telemetry[i].ActivePanelCount)
                    : 0f;
                float y = rect.yMax - rect.height * math.saturate(shadow);
                Handles.DrawSolidDisc(new Vector3(x, y, 0f), Vector3.forward, 2f);
            }
            Handles.EndGUI();
        }
    }
}
#endif
