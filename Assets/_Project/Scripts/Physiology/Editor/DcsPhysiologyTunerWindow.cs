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
        private Label _statusLabel;

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

            _chart = new VisualElement();
            _chart.style.height = 180;
            _chart.style.marginTop = 6;
            _chart.style.marginBottom = 8;
            _chart.generateVisualContent += GenerateChart;
            rootVisualElement.Add(_chart);

            _mValueStrictness = BuildSlider("M-Value Strictness", 0.05f, 8f);
            _offGassingMultiplier = BuildSlider("Off-gassing Multiplier", 0.05f, 16f);
            _narcosisThreshold = BuildSlider("Narcosis Threshold ATM", 1f, 12f);
            rootVisualElement.Add(_mValueStrictness);
            rootVisualElement.Add(_offGassingMultiplier);
            rootVisualElement.Add(_narcosisThreshold);

            _mValueStrictness.RegisterValueChangedCallback(_ => ApplyTuning());
            _offGassingMultiplier.RegisterValueChangedCallback(_ => ApplyTuning());
            _narcosisThreshold.RegisterValueChangedCallback(_ => ApplyTuning());

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

            for (int i = 0; i < TissueCount; i++)
            {
                if (_runtime.TryGetTissueTension(0, i, out float tension, out float mValue))
                {
                    _tensions[i] = tension;
                    _mValues[i] = mValue;
                }
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
