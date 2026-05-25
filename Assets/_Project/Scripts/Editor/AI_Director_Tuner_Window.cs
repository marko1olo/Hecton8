#if UNITY_EDITOR
using System.Globalization;
using Hecton8.AI;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class AI_Director_Tuner_Window : EditorWindow
    {
        private const int SampleCapacity = 128;
        private static readonly float[] TensionSamples = new float[SampleCapacity];
        private static readonly float[] BudgetSamples = new float[SampleCapacity];
        private static readonly float[] SpawnSamples = new float[SampleCapacity];

        private Slider _spawnRate;
        private Slider _frustumMargin;
        private Slider _budgetLow;
        private Slider _budgetUltra;
        private Slider _minRadius;
        private Slider _maxRadius;
        private Label _status;
        private VisualElement _graph;
        private int _cursor;

        [MenuItem("Hecton8/AI/AI Director Tuner")]
        public static void Open()
        {
            GetWindow<AI_Director_Tuner_Window>("AI Director");
        }

        private void OnEnable()
        {
            BuildUi();
            rootVisualElement.schedule.Execute(Refresh).Every(250);
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _status = new Label("Vault offline");
            _status.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(_status);

            _spawnRate = AddSlider("Base Spawn Rate / min", 0f, 12f);
            _frustumMargin = AddSlider("Frustum Margin", 0f, 64f);
            _budgetLow = AddSlider("Budget Low", 0.05f, 8f);
            _budgetUltra = AddSlider("Budget Ultra", 0.05f, 32f);
            _minRadius = AddSlider("Min Hidden Radius", 12f, 256f);
            _maxRadius = AddSlider("Max Hidden Radius", 16f, 1024f);

            Button reloadRules = new Button(ReloadRules)
            {
                text = "Reload CSV Rules"
            };
            reloadRules.style.marginTop = 6;
            rootVisualElement.Add(reloadRules);

            _graph = new VisualElement();
            _graph.style.height = 150;
            _graph.style.marginTop = 10;
            _graph.generateVisualContent += DrawGraph;
            rootVisualElement.Add(_graph);
        }

        private Slider AddSlider(string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(_ => ApplyTuningFromUi());
            rootVisualElement.Add(slider);
            return slider;
        }

        private void Refresh()
        {
            if (StressDrivenSpawnDirector.TryGetTuning(out DirectorTuningDTO tuning))
            {
                SetWithoutNotify(_spawnRate, tuning.BaseSpawnRatePerMinute);
                SetWithoutNotify(_frustumMargin, tuning.FrustumPlaneMarginMeters);
                SetWithoutNotify(_budgetLow, tuning.BudgetLow);
                SetWithoutNotify(_budgetUltra, tuning.BudgetUltra);
                SetWithoutNotify(_minRadius, tuning.MinHiddenRadiusMeters);
                SetWithoutNotify(_maxRadius, tuning.MaxHiddenRadiusMeters);
            }

            if (StressDrivenSpawnDirector.TryGetLatestTelemetry(out DirectorTelemetryEntry entry))
            {
                TensionSamples[_cursor] = math.saturate(entry.TensionIndex);
                BudgetSamples[_cursor] = math.saturate(entry.Budget / 8f);
                SpawnSamples[_cursor] = entry.Spawned > 0 ? 1f : 0f;
                _cursor = (_cursor + 1) & (SampleCapacity - 1);
                _status.text = "Frame " + entry.Frame +
                               " | candidates " + entry.CandidateCount +
                               " | owned " + entry.OwnedSlotCount +
                               " | us " + entry.ChainMicroseconds.ToString("0.0", CultureInfo.InvariantCulture);
            }
            else
            {
                _status.text = "Vault online, no telemetry sample";
            }

            _graph.MarkDirtyRepaint();
        }

        private void ApplyTuningFromUi()
        {
            if (!StressDrivenSpawnDirector.TryGetTuning(out DirectorTuningDTO tuning))
                return;

            tuning.BaseSpawnRatePerMinute = _spawnRate.value;
            tuning.FrustumPlaneMarginMeters = _frustumMargin.value;
            tuning.BudgetLow = _budgetLow.value;
            tuning.BudgetUltra = _budgetUltra.value;
            tuning.MinHiddenRadiusMeters = _minRadius.value;
            tuning.MaxHiddenRadiusMeters = _maxRadius.value;
            StressDrivenSpawnDirector.TrySetTuning(in tuning);
        }

        private void ReloadRules()
        {
            bool ok = StressDrivenSpawnDirector.TryReloadRulesCold();
            _status.text = ok ? "CSV rules reloaded" : "CSV reload rejected";
        }

        private static void SetWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        private void DrawGraph(MeshGenerationContext context)
        {
            Rect rect = _graph.contentRect;
            if (rect.width <= 2f || rect.height <= 2f)
                return;

            Painter2D painter = context.painter2D;
            painter.fillColor = new Color(0.08f, 0.09f, 0.1f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();

            DrawSeries(painter, rect, TensionSamples, new Color(0.85f, 0.25f, 0.18f), 0);
            DrawSeries(painter, rect, BudgetSamples, new Color(0.20f, 0.55f, 0.95f), 1);
            DrawSeries(painter, rect, SpawnSamples, new Color(0.2f, 0.9f, 0.45f), 2);
        }

        private void DrawSeries(Painter2D painter, Rect rect, float[] samples, Color color, int row)
        {
            painter.strokeColor = color;
            painter.lineWidth = row == 2 ? 3f : 2f;
            int count = samples.Length;
            Vector2 previous = default;
            bool hasPrevious = false;
            for (int i = 0; i < count; i++)
            {
                int index = (_cursor + i) & (SampleCapacity - 1);
                float x = rect.x + rect.width * (i / (float)(count - 1));
                float y = rect.yMax - (math.saturate(samples[index]) * (rect.height - 10f)) - 5f;
                if (row == 2 && samples[index] > 0f)
                {
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, rect.yMax - 5f));
                    painter.LineTo(new Vector2(x, rect.y + 5f));
                    painter.Stroke();
                    continue;
                }

                Vector2 current = new Vector2(x, y);
                if (row != 2 && hasPrevious)
                {
                    painter.BeginPath();
                    painter.MoveTo(previous);
                    painter.LineTo(current);
                    painter.Stroke();
                }

                previous = current;
                hasPrevious = true;
            }
        }
    }
}
#endif
