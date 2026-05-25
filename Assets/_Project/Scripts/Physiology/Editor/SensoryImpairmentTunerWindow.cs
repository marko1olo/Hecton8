#if UNITY_EDITOR
using Hecton8.Physiology;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physiology.Editor
{
    public sealed class SensoryImpairmentTunerWindow : EditorWindow
    {
        private const int SeriesCapacity = ShinobuSensoryImpairmentConstants.TelemetryFrameCount;
        private readonly float[] _hypoxiaSeries = new float[SeriesCapacity];
        private readonly float[] _narcosisSeries = new float[SeriesCapacity];
        private ShinobuSensoryImpairmentRuntime _runtime;
        private Label _status;
        private VisualElement _chart;
        private int _seriesCount;
        private Slider _hypoxia;
        private Slider _anoxia;
        private Slider _exponent;
        private Slider _narcosisStart;
        private Slider _narcosisFull;
        private Slider _moveDrift;
        private Slider _lookDrift;
        private Slider _latency;
        private Slider _complexNoise;

        [MenuItem("Hecton8/Physiology/Sensory Impairment Tuner")]
        public static void Open()
        {
            GetWindow<SensoryImpairmentTunerWindow>("Sensory Impairment");
        }

        public void CreateGUI()
        {
            RebindRuntime();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _status = new Label("No sensory impairment runtime");
            rootVisualElement.Add(_status);

            _chart = new VisualElement();
            _chart.style.height = 180;
            _chart.style.marginTop = 6;
            _chart.style.marginBottom = 8;
            _chart.generateVisualContent += GenerateChart;
            rootVisualElement.Add(_chart);

            _hypoxia = BuildSlider("Hypoxia PPO2", 0.09f, 0.35f);
            _anoxia = BuildSlider("Anoxia PPO2", 0.02f, 0.14f);
            _exponent = BuildSlider("Vignette Polynomial", 1f, 5f);
            _narcosisStart = BuildSlider("Narcosis Start ATM", 1f, 12f);
            _narcosisFull = BuildSlider("Narcosis Full ATM", 1.25f, 16f);
            _moveDrift = BuildSlider("Max Narcosis Drift Scalar", 0f, 1f);
            _lookDrift = BuildSlider("Look Drift Degrees", 0f, 90f);
            _latency = BuildSlider("Latency Milliseconds", 0f, 500f);
            _complexNoise = BuildSlider("Complex Noise Scale", 0f, 2f);

            rootVisualElement.Add(_hypoxia);
            rootVisualElement.Add(_anoxia);
            rootVisualElement.Add(_exponent);
            rootVisualElement.Add(_narcosisStart);
            rootVisualElement.Add(_narcosisFull);
            rootVisualElement.Add(_moveDrift);
            rootVisualElement.Add(_lookDrift);
            rootVisualElement.Add(_latency);
            rootVisualElement.Add(_complexNoise);

            _hypoxia.RegisterValueChangedCallback(_ => ApplyTuning());
            _anoxia.RegisterValueChangedCallback(_ => ApplyTuning());
            _exponent.RegisterValueChangedCallback(_ => ApplyTuning());
            _narcosisStart.RegisterValueChangedCallback(_ => ApplyTuning());
            _narcosisFull.RegisterValueChangedCallback(_ => ApplyTuning());
            _moveDrift.RegisterValueChangedCallback(_ => ApplyTuning());
            _lookDrift.RegisterValueChangedCallback(_ => ApplyTuning());
            _latency.RegisterValueChangedCallback(_ => ApplyTuning());
            _complexNoise.RegisterValueChangedCallback(_ => ApplyTuning());

            rootVisualElement.schedule.Execute(Refresh).Every(100);
        }

        private void OnFocus()
        {
            RebindRuntime();
        }

        private void OnHierarchyChange()
        {
            RebindRuntime();
        }

        private void RebindRuntime()
        {
            _runtime = UnityEngine.Object.FindAnyObjectByType<ShinobuSensoryImpairmentRuntime>();
        }

        private static Slider BuildSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            return slider;
        }

        private void Refresh()
        {
            if (_runtime == null)
            {
                _status.text = "No sensory impairment runtime";
                return;
            }

            if (_runtime.TryGetTuning(out SensoryImpairmentTuningDTO tuning))
            {
                _hypoxia.SetValueWithoutNotify(tuning.HypoxiaPartialPressureAtm);
                _anoxia.SetValueWithoutNotify(tuning.AnoxiaPartialPressureAtm);
                _exponent.SetValueWithoutNotify(tuning.HypoxiaCurveExponent);
                _narcosisStart.SetValueWithoutNotify(tuning.NarcosisStartAtm);
                _narcosisFull.SetValueWithoutNotify(tuning.NarcosisFullAtm);
                _moveDrift.SetValueWithoutNotify(tuning.MaxNarcosisDriftScalar);
                _lookDrift.SetValueWithoutNotify(tuning.MaxLookDriftDegrees);
                _latency.SetValueWithoutNotify(tuning.MaxInputLatencyMilliseconds);
                _complexNoise.SetValueWithoutNotify(tuning.ComplexDriftScale);
            }

            if (_runtime.TryGetSensoryImpairment(out SensoryImpairmentDTO impairment) &&
                _runtime.TryGetLatestTelemetry(out SensoryImpairmentTelemetryEntry telemetry))
            {
                _seriesCount = _runtime.CopyTelemetrySeriesForEditor(_hypoxiaSeries, _narcosisSeries);
                _chart.MarkDirtyRepaint();
                _status.text =
                    $"Hypoxia {impairment.HypoxiaVignette01:0.00} | Narcosis {impairment.NarcosisDrift01:0.00} | Lag {impairment.InputLatencyMilliseconds:0} ms | {telemetry.ExecutionMicroseconds:0.0} us";
            }
            else
            {
                _status.text = "Sensory impairment vault unavailable";
            }
        }

        private void ApplyTuning()
        {
            if (_runtime == null || !_runtime.TryGetTuning(out SensoryImpairmentTuningDTO tuning))
                return;

            tuning.HypoxiaPartialPressureAtm = math.max(_anoxia.value + 0.01f, _hypoxia.value);
            tuning.AnoxiaPartialPressureAtm = _anoxia.value;
            tuning.HypoxiaCurveExponent = _exponent.value;
            tuning.NarcosisStartAtm = _narcosisStart.value;
            tuning.NarcosisFullAtm = math.max(_narcosisStart.value + 0.25f, _narcosisFull.value);
            tuning.MaxNarcosisDriftScalar = _moveDrift.value;
            tuning.MaxLookDriftDegrees = _lookDrift.value;
            tuning.MaxInputLatencyMilliseconds = _latency.value;
            tuning.ComplexDriftScale = _complexNoise.value;
            _runtime.SetEditorTuning(tuning);
        }

        private void GenerateChart(MeshGenerationContext context)
        {
            Rect rect = _chart.contentRect;
            if (rect.width <= 1f || rect.height <= 1f || _seriesCount <= 1)
                return;

            Painter2D painter = context.painter2D;
            DrawSeries(painter, rect, _hypoxiaSeries, _seriesCount, new Color(0.84f, 0.12f, 0.08f, 1f));
            DrawSeries(painter, rect, _narcosisSeries, _seriesCount, new Color(0.08f, 0.72f, 0.85f, 1f));
        }

        private static void DrawSeries(Painter2D painter, Rect rect, float[] series, int count, Color color)
        {
            painter.strokeColor = color;
            painter.lineWidth = 2f;
            painter.BeginPath();
            for (int i = 0; i < count; i++)
            {
                float x = rect.xMin + rect.width * (i / (float)(count - 1));
                float y = rect.yMax - rect.height * Mathf.Clamp01(series[i]);
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();
        }
    }
}
#endif
