#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Power
{
    public sealed unsafe class SolverConvergenceXRayWindow : EditorWindow
    {
        private const int SampleCount = SubmarineOsThermalGridRuntime.TelemetryFrameCount;

        private readonly float[] _residualSamples = new float[SampleCount];
        private Label _statusLabel;
        private Label _telemetryLabel;
        private Slider _toleranceSlider;
        private Slider _omegaSlider;
        private Slider _toleranceMultiplierSlider;
        private ResidualGraphElement _graph;
        private int _writeCursor;
        private bool _suppress;

        [MenuItem("Hecton8/Power/Solver Convergence X-Ray")]
        public static void Open()
        {
            SolverConvergenceXRayWindow window = GetWindow<SolverConvergenceXRayWindow>();
            window.titleContent = new GUIContent("Solver Convergence X-Ray");
            window.minSize = new Vector2(420f, 260f);
            window.Show();
        }

        private void OnEnable()
        {
            BuildUi();
            EditorApplication.update += Refresh;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Refresh;
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

            _telemetryLabel = new Label("Residual 0 | Omega 1.00 | Tolerance 0.001 | Iter 0 | Fault 0");
            rootVisualElement.Add(_telemetryLabel);

            _graph = new ResidualGraphElement(_residualSamples);
            _graph.style.height = 96f;
            _graph.style.marginTop = 8f;
            _graph.style.marginBottom = 8f;
            rootVisualElement.Add(_graph);

            _toleranceSlider = new Slider("Base Jacobi Tolerance", 0.0001f, 0.5f) { showInputField = true };
            _toleranceSlider.RegisterValueChangedCallback(OnToleranceChanged);
            rootVisualElement.Add(_toleranceSlider);
            _omegaSlider = new Slider("Base Omega Factor", 0.25f, 1.1f) { showInputField = true };
            _omegaSlider.RegisterValueChangedCallback(OnOmegaChanged);
            rootVisualElement.Add(_omegaSlider);
            _toleranceMultiplierSlider = new Slider("Tolerance Multiplier", 0.1f, 64f) { showInputField = true };
            _toleranceMultiplierSlider.RegisterValueChangedCallback(OnToleranceMultiplierChanged);
            rootVisualElement.Add(_toleranceMultiplierSlider);

            Button mockButton = new Button(TriggerOscillatorMock) { text = "Schedule Oscillator Mock" };
            rootVisualElement.Add(mockButton);
        }

        private void Refresh()
        {
            SubmarineOsThermalGridRuntime runtime = SubmarineOsThermalGridRuntime.Active;
            if (runtime == null || !runtime.IsInitialized ||
                !runtime.TryGetConvergenceReadback(out SolverConvergenceStateDTO state, out ThermalPowerGridTelemetrySnapshot telemetry))
            {
                _statusLabel.text = "Runtime: offline";
                return;
            }

            _statusLabel.text = "Runtime: online";
            _telemetryLabel.text =
                $"Residual {telemetry.JacobiResidual:0.000000} | Omega {telemetry.SolverOmega:0.00} | Tolerance {telemetry.TargetTolerance:0.000000} | Iter {telemetry.IterationCount} | Fault {state.FaultFlags}";

            _residualSamples[_writeCursor] = Mathf.Max(0f, telemetry.JacobiResidual);
            _writeCursor = (_writeCursor + 1) % SampleCount;
            _graph.MarkDirtyRepaint();

            if (runtime.TryGetTuningPointer(out SubmarineThermalGridTuningDTO* tuning))
            {
                _suppress = true;
                _toleranceSlider.SetValueWithoutNotify(tuning->JacobiTolerance);
                _omegaSlider.SetValueWithoutNotify(tuning->BaseOmegaFactor);
                _toleranceMultiplierSlider.SetValueWithoutNotify(tuning->ToleranceMultiplier);
                _suppress = false;
            }
        }

        private void OnToleranceChanged(ChangeEvent<float> evt)
        {
            if (_suppress)
                return;

            SubmarineOsThermalGridRuntime runtime = SubmarineOsThermalGridRuntime.Active;
            if (runtime == null || !runtime.TryGetTuningPointer(out SubmarineThermalGridTuningDTO* tuning))
                return;

            tuning->JacobiTolerance = Mathf.Max(0.0001f, evt.newValue);
        }

        private void OnOmegaChanged(ChangeEvent<float> evt)
        {
            if (_suppress)
                return;

            SubmarineOsThermalGridRuntime runtime = SubmarineOsThermalGridRuntime.Active;
            if (runtime == null || !runtime.TryGetTuningPointer(out SubmarineThermalGridTuningDTO* tuning))
                return;

            tuning->BaseOmegaFactor = Mathf.Clamp(evt.newValue, 0.25f, 1.1f);
        }

        private void OnToleranceMultiplierChanged(ChangeEvent<float> evt)
        {
            if (_suppress)
                return;

            SubmarineOsThermalGridRuntime runtime = SubmarineOsThermalGridRuntime.Active;
            if (runtime == null || !runtime.TryGetTuningPointer(out SubmarineThermalGridTuningDTO* tuning))
                return;

            tuning->ToleranceMultiplier = Mathf.Clamp(evt.newValue, 0.1f, 64f);
        }

        private static void TriggerOscillatorMock()
        {
            SubmarineOsThermalGridRuntime.Active?.ScheduleEmergencyMockOscillatorGrid(default);
        }

        private sealed class ResidualGraphElement : VisualElement
        {
            private readonly float[] _samples;

            public ResidualGraphElement(float[] samples)
            {
                _samples = samples;
                generateVisualContent += DrawGraph;
                style.backgroundColor = new Color(0.02f, 0.025f, 0.03f, 1f);
            }

            private void DrawGraph(MeshGenerationContext context)
            {
                Rect r = contentRect;
                if (_samples == null || _samples.Length <= 1 || r.width <= 1f || r.height <= 1f)
                    return;

                float max = 0.001f;
                for (int i = 0; i < _samples.Length; i++)
                    max = Mathf.Max(max, _samples[i]);

                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.5f;
                painter.strokeColor = new Color(1f, 0.18f, 0.16f, 1f);
                painter.BeginPath();
                for (int i = 0; i < _samples.Length; i++)
                {
                    float x = r.xMin + (r.width * i / (_samples.Length - 1));
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
