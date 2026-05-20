using Hecton8.Construction;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    /// <summary>
    /// UI Toolkit facade for SHINOBU_222 Vault-backed drainage tuning.
    /// </summary>
    public sealed class SumpPumpPipeGridTunerWindow : EditorWindow
    {
        private Label _statusLabel;
        private IntegerField _frameField;
        private FloatField _evacuatedField;
        private IntegerField _pumpsField;
        private FloatField _pressureField;
        private IntegerField _solverMicrosField;
        private Slider _conductanceSlider;
        private Slider _pumpPowerSlider;
        private Slider _jacobiSlider;
        private Button _mockButton;
        private DrainageTuningDTO _cachedTuning;
        private double _nextReadoutRefresh;

        [MenuItem("HECTON-8/Base Drainage Tuner")]
        public static void Open()
        {
            SumpPumpPipeGridTunerWindow window = GetWindow<SumpPumpPipeGridTunerWindow>();
            window.titleContent = new GUIContent("Base Drainage");
            window.minSize = new Vector2(360f, 240f);
        }

        private void OnEnable()
        {
            BuildUi();
        }

        private void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextReadoutRefresh)
                return;

            _nextReadoutRefresh = now + 0.25d;
            RefreshReadout();
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10f;
            rootVisualElement.style.paddingRight = 10f;
            rootVisualElement.style.paddingTop = 10f;
            rootVisualElement.style.paddingBottom = 10f;

            _statusLabel = new Label("Runtime: PENDING");
            rootVisualElement.Add(_statusLabel);
            _frameField = CreateReadoutInteger("Frame");
            _evacuatedField = CreateReadoutFloat("Evacuated m3");
            _pumpsField = CreateReadoutInteger("Active Pumps");
            _pressureField = CreateReadoutFloat("Average Pressure");
            _solverMicrosField = CreateReadoutInteger("Solver us");
            rootVisualElement.Add(_frameField);
            rootVisualElement.Add(_evacuatedField);
            rootVisualElement.Add(_pumpsField);
            rootVisualElement.Add(_pressureField);
            rootVisualElement.Add(_solverMicrosField);

            SumpPumpPipeGridRuntime.TryGetTuning(out _cachedTuning);
            _conductanceSlider = CreateSlider("Base Pipe Conductance", 0.001f, 0.4f, _cachedTuning.BasePipeConductance, OnConductanceChanged);
            _pumpPowerSlider = CreateSlider("Pump Power Draw", 0f, 750f, _cachedTuning.PumpPowerDraw, OnPumpPowerChanged);
            _jacobiSlider = CreateSlider("Jacobi Smoothing", 0.05f, 1f, _cachedTuning.JacobiSmoothingFactor, OnJacobiChanged);
            rootVisualElement.Add(_conductanceSlider);
            rootVisualElement.Add(_pumpPowerSlider);
            rootVisualElement.Add(_jacobiSlider);

            _mockButton = new Button(GenerateMock) { text = "Generate Mock Drainage Network" };
            rootVisualElement.Add(_mockButton);
        }

        private static Slider CreateSlider(string label, float min, float max, float value, EventCallback<ChangeEvent<float>> callback)
        {
            Slider slider = new Slider(label, min, max) { value = value };
            slider.RegisterValueChangedCallback(callback);
            return slider;
        }

        private static IntegerField CreateReadoutInteger(string label)
        {
            IntegerField field = new IntegerField(label);
            field.SetEnabled(false);
            field.SetValueWithoutNotify(0);
            return field;
        }

        private static FloatField CreateReadoutFloat(string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            field.SetValueWithoutNotify(0f);
            return field;
        }

        private void OnConductanceChanged(ChangeEvent<float> evt)
        {
            _cachedTuning.BasePipeConductance = Mathf.Max(0.001f, evt.newValue);
            SumpPumpPipeGridRuntime.SetTuning(in _cachedTuning);
        }

        private void OnPumpPowerChanged(ChangeEvent<float> evt)
        {
            _cachedTuning.PumpPowerDraw = Mathf.Max(0f, evt.newValue);
            SumpPumpPipeGridRuntime.SetTuning(in _cachedTuning);
        }

        private void OnJacobiChanged(ChangeEvent<float> evt)
        {
            _cachedTuning.JacobiSmoothingFactor = Mathf.Clamp01(evt.newValue);
            SumpPumpPipeGridRuntime.SetTuning(in _cachedTuning);
        }

        private void GenerateMock()
        {
            SumpPumpPipeGridRuntime runtime = FindFirstObjectByType<SumpPumpPipeGridRuntime>();
            if (runtime != null)
                runtime.GenerateMockDrainageNetwork();
        }

        private void RefreshReadout()
        {
            if (_statusLabel == null || _frameField == null || _evacuatedField == null || _pumpsField == null || _pressureField == null || _solverMicrosField == null)
                return;

            bool hasRuntime = SumpPumpPipeGridRuntime.HasActiveRuntime;
            _statusLabel.text = hasRuntime ? "Runtime: ACTIVE" : "Runtime: MISSING";

            if (SumpPumpPipeGridRuntime.TryGetLatestTelemetry(out DrainageTelemetryEntry entry))
            {
                _frameField.SetValueWithoutNotify(ClampUIntToInt(entry.FrameIndex));
                _evacuatedField.SetValueWithoutNotify(entry.FrameEvacuatedM3);
                _pumpsField.SetValueWithoutNotify(ClampUIntToInt(entry.ActivePumpCount));
                _pressureField.SetValueWithoutNotify(entry.AveragePressure);
                _solverMicrosField.SetValueWithoutNotify(ClampUIntToInt(entry.SolverWallMicroseconds));
            }
            else
            {
                _frameField.SetValueWithoutNotify(0);
                _evacuatedField.SetValueWithoutNotify(0f);
                _pumpsField.SetValueWithoutNotify(0);
                _pressureField.SetValueWithoutNotify(0f);
                _solverMicrosField.SetValueWithoutNotify(0);
            }
        }

        private static int ClampUIntToInt(uint value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
