#if UNITY_EDITOR
using Hecton8.Physiology;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physiology.Editor
{
    public sealed class DcsPhysiologyTunerWindow : EditorWindow
    {
        private const int TissueCount = ShinobuPhysiologyConstants.TissueCompartmentCount;
        private readonly float[] _tensions = new float[TissueCount];
        private readonly float[] _mValues = new float[TissueCount];

        private ShinobuPhysiologyRuntime _runtime;
        private VisualElement _chart;
        private Slider _mValueStrictness;
        private Slider _offGassingMultiplier;
        private Slider _narcosisThreshold;
        private Slider _cnsToxicityRate;
        private Slider _hypoxiaLimit;
        private Slider _anoxiaLimit;
        private Slider _co2ToxicityLimit;
        private Label _statusLabel;
        private Label _gasStatusLabel;
        private Label _telemetryStatusLabel;

        [MenuItem("Hecton/Physiology/DCS Physiology Tuner")]
        public static void Open()
        {
            GetWindow<DcsPhysiologyTunerWindow>("DCS Physiology Tuner");
        }

        public void CreateGUI()
        {
            RebindRuntime();

            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _statusLabel = new Label("No physiology runtime");
            rootVisualElement.Add(_statusLabel);
            _gasStatusLabel = new Label("Gas physiology: unavailable");
            rootVisualElement.Add(_gasStatusLabel);
            _telemetryStatusLabel = new Label("Telemetry ring: unavailable");
            rootVisualElement.Add(_telemetryStatusLabel);

            _chart = new VisualElement();
            _chart.style.height = 180;
            _chart.style.marginTop = 6;
            _chart.style.marginBottom = 8;
            _chart.generateVisualContent += GenerateChart;
            rootVisualElement.Add(_chart);

            _mValueStrictness = BuildSlider("M-Value Strictness", 0.05f, 8f);
            _offGassingMultiplier = BuildSlider("Off-gassing Multiplier", 0.05f, 16f);
            _narcosisThreshold = BuildSlider("Narcosis Threshold ATM", 1f, 12f);
            _cnsToxicityRate = BuildSlider("CNS Toxicity Rate", 0.001f, 0.25f);
            _hypoxiaLimit = BuildSlider("Hypoxia PPO2 Limit", 0.09f, 0.35f);
            _anoxiaLimit = BuildSlider("Anoxia PPO2 Limit", 0.02f, 0.14f);
            _co2ToxicityLimit = BuildSlider("CO2 Toxicity ATM", 0.005f, 0.2f);
            rootVisualElement.Add(_mValueStrictness);
            rootVisualElement.Add(_offGassingMultiplier);
            rootVisualElement.Add(_narcosisThreshold);
            rootVisualElement.Add(_cnsToxicityRate);
            rootVisualElement.Add(_hypoxiaLimit);
            rootVisualElement.Add(_anoxiaLimit);
            rootVisualElement.Add(_co2ToxicityLimit);

            _mValueStrictness.RegisterValueChangedCallback(_ => ApplyTuning());
            _offGassingMultiplier.RegisterValueChangedCallback(_ => ApplyTuning());
            _narcosisThreshold.RegisterValueChangedCallback(_ => ApplyTuning());
            _cnsToxicityRate.RegisterValueChangedCallback(_ => ApplyTuning());
            _hypoxiaLimit.RegisterValueChangedCallback(_ => ApplyTuning());
            _anoxiaLimit.RegisterValueChangedCallback(_ => ApplyTuning());
            _co2ToxicityLimit.RegisterValueChangedCallback(_ => ApplyTuning());

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
            _runtime = UnityEngine.Object.FindFirstObjectByType<ShinobuPhysiologyRuntime>();
        }

        private static Slider BuildSlider(string label, float lowValue, float highValue)
        {
            Slider slider = new Slider(label, lowValue, highValue);
            slider.showInputField = true;
            return slider;
        }

        private void Refresh()
        {
            if (_runtime == null)
            {
                _statusLabel.text = "No physiology runtime";
                return;
            }

            if (_runtime.TryGetTuning(out PhysiologyTuningDTO tuning))
            {
                _mValueStrictness.SetValueWithoutNotify(tuning.BendsRiskScale);
                _offGassingMultiplier.SetValueWithoutNotify(tuning.HaldaneTimeScale);
                _narcosisThreshold.SetValueWithoutNotify(tuning.NarcosisStartAtm);
            }

            if (_runtime.TryGetGasTuning(out GasPhysiologyTuningDTO gasTuning))
            {
                _cnsToxicityRate.SetValueWithoutNotify(gasTuning.CnsAccumulationRate);
                _hypoxiaLimit.SetValueWithoutNotify(gasTuning.HypoxiaPartialPressureAtm);
                _anoxiaLimit.SetValueWithoutNotify(gasTuning.AnoxiaPartialPressureAtm);
                _co2ToxicityLimit.SetValueWithoutNotify(gasTuning.CarbonDioxideToxicityStartAtm);
            }

            for (int i = 0; i < TissueCount; i++)
            {
                if (_runtime.TryGetTissueTension(0, i, out float tension, out float mValue))
                {
                    _tensions[i] = tension;
                    _mValues[i] = mValue;
                }
            }

            if (_runtime.TryGetGasPhysiologyState(0, out GasPhysiologyStateDTO gas))
            {
                _gasStatusLabel.text =
                    $"Gas PPO2 {gas.OxygenPartialPressure:0.000} atm | PPN2 {gas.NitrogenPartialPressure:0.000} atm | PPCO2 {gas.CarbonDioxidePartialPressure:0.000} atm | CNS {gas.CnsToxicity01:0.00}";
            }
            else
            {
                _gasStatusLabel.text = "Gas physiology: unavailable";
            }

            if (_runtime.TryGetLatestTelemetry(out PhysiologyTelemetryEntry telemetry))
            {
                _telemetryStatusLabel.text =
                    $"Telemetry frame {telemetry.Frame} | depth {telemetry.DepthMeters:0.0} m | ambient {telemetry.AmbientPressureAtm:0.00} atm | supersat {telemetry.SupersaturationScalar:0.00} | {telemetry.ExecutionMicroseconds:0.0} us";
            }
            else
            {
                _telemetryStatusLabel.text = "Telemetry ring: unavailable";
            }

            _statusLabel.text = "Vault tissue compartments: live";
            _chart.MarkDirtyRepaint();
        }

        private void ApplyTuning()
        {
            if (_runtime == null || !_runtime.TryGetTuning(out PhysiologyTuningDTO tuning))
                return;

            tuning.BendsRiskScale = _mValueStrictness.value;
            tuning.HaldaneTimeScale = _offGassingMultiplier.value;
            tuning.NarcosisStartAtm = _narcosisThreshold.value;
            _runtime.SetEditorTuning(tuning);

            if (_runtime.TryGetGasTuning(out GasPhysiologyTuningDTO gasTuning))
            {
                gasTuning.CnsAccumulationRate = _cnsToxicityRate.value;
                gasTuning.NarcosisStartAtm = _narcosisThreshold.value;
                gasTuning.NarcosisFullAtm = math.max(_narcosisThreshold.value + 0.25f, gasTuning.NarcosisFullAtm);
                gasTuning.HypoxiaPartialPressureAtm = math.max(_anoxiaLimit.value + 0.01f, _hypoxiaLimit.value);
                gasTuning.AnoxiaPartialPressureAtm = _anoxiaLimit.value;
                gasTuning.CarbonDioxideToxicityStartAtm = _co2ToxicityLimit.value;
                _runtime.SetEditorGasTuning(gasTuning);
            }
        }

        private void GenerateChart(MeshGenerationContext context)
        {
            Rect rect = _chart.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Painter2D painter = context.painter2D;
            float max = 1f;
            for (int i = 0; i < TissueCount; i++)
                max = math.max(max, math.max(_tensions[i], _mValues[i]));

            float barStep = rect.width / TissueCount;
            float barWidth = math.max(2f, barStep - 2f);
            for (int i = 0; i < TissueCount; i++)
            {
                float x = rect.x + i * barStep + 1f;
                float tensionHeight = math.saturate(_tensions[i] / max) * rect.height;
                float mValueHeight = math.saturate(_mValues[i] / max) * rect.height;
                DrawRect(painter, new Rect(x, rect.yMax - tensionHeight, barWidth, tensionHeight), new Color(0.84f, 0.18f, 0.16f, 1f));
                DrawRect(painter, new Rect(x, rect.yMax - mValueHeight, barWidth, 2f), new Color(0.1f, 0.8f, 0.65f, 1f));
            }
        }

        private static void DrawRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
#endif
