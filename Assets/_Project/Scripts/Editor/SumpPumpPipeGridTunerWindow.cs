using System.Globalization;
using System.Text;
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
        private Label _telemetryLabel;
        private Slider _conductanceSlider;
        private Slider _pumpPowerSlider;
        private Slider _jacobiSlider;
        private Button _mockButton;
        private DrainageTuningDTO _cachedTuning;
        private readonly StringBuilder _readoutBuilder = new StringBuilder(160);
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
            _telemetryLabel = new Label("Telemetry: PENDING");
            rootVisualElement.Add(_statusLabel);
            rootVisualElement.Add(_telemetryLabel);

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
            if (_statusLabel == null || _telemetryLabel == null)
                return;

            bool hasRuntime = SumpPumpPipeGridRuntime.HasActiveRuntime;
            _statusLabel.text = hasRuntime ? "Runtime: ACTIVE" : "Runtime: MISSING";

            if (SumpPumpPipeGridRuntime.TryGetLatestTelemetry(out DrainageTelemetryEntry entry))
            {
                _readoutBuilder.Clear();
                _readoutBuilder.Append("Frame ");
                _readoutBuilder.Append(entry.FrameIndex);
                _readoutBuilder.Append(" | Evacuated ");
                _readoutBuilder.Append(entry.FrameEvacuatedM3.ToString("0.000", CultureInfo.InvariantCulture));
                _readoutBuilder.Append(" m3 | Pumps ");
                _readoutBuilder.Append(entry.ActivePumpCount);
                _readoutBuilder.Append(" | Pressure ");
                _readoutBuilder.Append(entry.AveragePressure.ToString("0.000", CultureInfo.InvariantCulture));
                _readoutBuilder.Append(" | us ");
                _readoutBuilder.Append(entry.SolverWallMicroseconds);
                _telemetryLabel.text = _readoutBuilder.ToString();
            }
            else
            {
                _telemetryLabel.text = "Telemetry: PENDING";
            }
        }
    }
}
