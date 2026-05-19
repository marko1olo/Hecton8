#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Thermodynamics
{
    public sealed class AbyssalHeatTunerWindow : EditorWindow
    {
        private Slider _ambientSlider;
        private Slider _conductivitySlider;
        private Slider _convectionSlider;
        private Label _statusLabel;
        private Label _telemetryLabel;
        private HeatGraphElement _graph;
        private bool _suppress;

        [MenuItem("Hecton8/Thermodynamics/Abyssal Heat Tuner")]
        public static void Open()
        {
            AbyssalHeatTunerWindow window = GetWindow<AbyssalHeatTunerWindow>();
            window.titleContent = new GUIContent("Abyssal Heat Tuner");
            window.minSize = new Vector2(360f, 260f);
            window.Show();
        }

        private void OnEnable()
        {
            BuildUi();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10f;
            rootVisualElement.style.paddingRight = 10f;
            rootVisualElement.style.paddingTop = 10f;
            rootVisualElement.style.paddingBottom = 10f;

            _statusLabel = new Label("Runtime: offline");
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(_statusLabel);

            _telemetryLabel = new Label("MaxTemp 0.0 C | Sources 0 | Jacobi 0 | Solver 0us");
            rootVisualElement.Add(_telemetryLabel);

            _graph = new HeatGraphElement();
            _graph.style.height = 96f;
            _graph.style.marginTop = 8f;
            _graph.style.marginBottom = 8f;
            rootVisualElement.Add(_graph);

            _ambientSlider = CreateSlider("Ambient Temperature C", -5f, 40f, OnAmbientChanged);
            _conductivitySlider = CreateSlider("Water Thermal Conductivity", 0.001f, 1.5f, OnConductivityChanged);
            _convectionSlider = CreateSlider("Convection Speed", 0f, 0.2f, OnConvectionChanged);
            rootVisualElement.Add(_ambientSlider);
            rootVisualElement.Add(_conductivitySlider);
            rootVisualElement.Add(_convectionSlider);

            Button dumpButton = new Button(RefreshFromRuntime) { text = "Refresh Vault Readback" };
            rootVisualElement.Add(dumpButton);
        }

        private static Slider CreateSlider(string label, float min, float max, EventCallback<ChangeEvent<float>> callback)
        {
            Slider slider = new Slider(label, min, max) { showInputField = true };
            slider.RegisterValueChangedCallback(callback);
            return slider;
        }

        private void OnEditorUpdate()
        {
            RefreshFromRuntime();
        }

        private void RefreshFromRuntime()
        {
            AbyssalThermodynamicsSolver runtime = AbyssalThermodynamicsSolver.ActiveRuntimeInstance;
            if (runtime == null || !runtime.IsInitialized)
            {
                _statusLabel.text = "Runtime: offline";
                _graph.ClearSamples();
                return;
            }

            _statusLabel.text = "Runtime: online";
            if (runtime.TryReadTuning(out ThermalGridTuningDTO tuning))
            {
                _suppress = true;
                _ambientSlider.SetValueWithoutNotify(tuning.AmbientTemperatureCelsius);
                _conductivitySlider.SetValueWithoutNotify(tuning.WaterThermalConductivity);
                _convectionSlider.SetValueWithoutNotify(tuning.ConvectionSpeed);
                _suppress = false;
            }

            if (runtime.TryReadTelemetry(0, out ThermalTelemetryEntry latest))
            {
                _telemetryLabel.text =
                    $"MaxTemp {latest.MaxTemperatureCelsius:0.0} C | Sources {latest.ActiveSourceCount} | Jacobi {latest.JacobiIterations} | Solver {latest.SolverMicroseconds:0}us";
            }

            _graph.Refresh(runtime);
        }

        private void OnAmbientChanged(ChangeEvent<float> evt)
        {
            if (!_suppress)
                MutateTuning(evt.newValue, null, null);
        }

        private void OnConductivityChanged(ChangeEvent<float> evt)
        {
            if (!_suppress)
                MutateTuning(null, evt.newValue, null);
        }

        private void OnConvectionChanged(ChangeEvent<float> evt)
        {
            if (!_suppress)
                MutateTuning(null, null, evt.newValue);
        }

        private static void MutateTuning(float? ambient, float? conductivity, float? convection)
        {
            AbyssalThermodynamicsSolver runtime = AbyssalThermodynamicsSolver.ActiveRuntimeInstance;
            if (runtime == null || !runtime.TryReadTuning(out ThermalGridTuningDTO tuning))
                return;

            if (ambient.HasValue)
                tuning.AmbientTemperatureCelsius = ambient.Value;
            if (conductivity.HasValue)
                tuning.WaterThermalConductivity = conductivity.Value;
            if (convection.HasValue)
                tuning.ConvectionSpeed = convection.Value;

            runtime.TryWriteTuning(tuning);
        }

        private sealed class HeatGraphElement : VisualElement
        {
            private const int SampleCount = 300;
            private readonly float[] _samples = new float[SampleCount];
            private int _validSamples;

            public HeatGraphElement()
            {
                generateVisualContent += DrawGraph;
                style.backgroundColor = new Color(0.02f, 0.025f, 0.03f, 1f);
            }

            public void ClearSamples()
            {
                _validSamples = 0;
                MarkDirtyRepaint();
            }

            public void Refresh(AbyssalThermodynamicsSolver runtime)
            {
                _validSamples = 0;
                for (int i = 0; i < SampleCount; i++)
                {
                    int offset = SampleCount - 1 - i;
                    if (runtime.TryReadTelemetry(offset, out ThermalTelemetryEntry entry))
                    {
                        _samples[i] = entry.MaxTemperatureCelsius;
                        _validSamples++;
                    }
                    else
                    {
                        _samples[i] = 0f;
                    }
                }

                MarkDirtyRepaint();
            }

            private void DrawGraph(MeshGenerationContext context)
            {
                Rect r = contentRect;
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.5f;
                painter.strokeColor = new Color(1f, 0.86f, 0.26f, 1f);

                if (_validSamples <= 1 || r.width <= 1f || r.height <= 1f)
                    return;

                float max = 1f;
                for (int i = 0; i < SampleCount; i++)
                    max = Mathf.Max(max, _samples[i]);

                painter.BeginPath();
                for (int i = 0; i < SampleCount; i++)
                {
                    float x = r.xMin + (r.width * i / (SampleCount - 1));
                    float y = r.yMax - (Mathf.Clamp01(_samples[i] / max) * r.height);
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }
    }
}
#endif
