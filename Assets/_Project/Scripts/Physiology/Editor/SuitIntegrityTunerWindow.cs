#if UNITY_EDITOR
using Hecton8.Core.Contracts.Physiology;
using Hecton8.Physiology;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physiology.Editor
{
    public sealed class SuitIntegrityTunerWindow : EditorWindow
    {
        private ShinobuSuitIntegrityRuntime _runtime;
        private VisualElement _chart;
        private Slider _quality;
        private Slider _warning;
        private Slider _buckling;
        private Slider _visualGain;
        private Slider _mockDepth;
        private Label _status;
        private Label _telemetry;

        [MenuItem("Hecton8/Physiology/Suit Integrity Tuner")]
        public static void Open()
        {
            GetWindow<SuitIntegrityTunerWindow>("Suit Integrity");
        }

        public void CreateGUI()
        {
            RebindRuntime();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _status = new Label("No suit integrity runtime");
            _telemetry = new Label("Telemetry unavailable");
            rootVisualElement.Add(_status);
            rootVisualElement.Add(_telemetry);

            _chart = new VisualElement();
            _chart.style.height = 150;
            _chart.style.marginTop = 6;
            _chart.style.marginBottom = 8;
            _chart.generateVisualContent += DrawChart;
            rootVisualElement.Add(_chart);

            _quality = BuildSlider("Global Quality Weight", 0f, 1f);
            _warning = BuildSlider("Warning Overpressure", 0f, 2f);
            _buckling = BuildSlider("Buckling Overpressure", 0f, 4f);
            _visualGain = BuildSlider("Visual Deformation Gain", 0f, 4f);
            _mockDepth = BuildSlider("Mock Max Depth M", 100f, 8000f);
            rootVisualElement.Add(_quality);
            rootVisualElement.Add(_warning);
            rootVisualElement.Add(_buckling);
            rootVisualElement.Add(_visualGain);
            rootVisualElement.Add(_mockDepth);

            _quality.RegisterValueChangedCallback(_ => ApplyTuning());
            _warning.RegisterValueChangedCallback(_ => ApplyTuning());
            _buckling.RegisterValueChangedCallback(_ => ApplyTuning());
            _visualGain.RegisterValueChangedCallback(_ => ApplyTuning());
            _mockDepth.RegisterValueChangedCallback(_ => ApplyTuning());
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
            _runtime = Object.FindAnyObjectByType<ShinobuSuitIntegrityRuntime>();
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
                _status.text = "No suit integrity runtime";
                return;
            }

            if (_runtime.TryGetTuning(out SuitIntegrityTuningDTO tuning))
            {
                _quality.SetValueWithoutNotify(tuning.GlobalQualityWeight);
                _warning.SetValueWithoutNotify(tuning.WarningOverpressure);
                _buckling.SetValueWithoutNotify(tuning.BuckleOverpressure);
                _visualGain.SetValueWithoutNotify(tuning.VisualDeformationGain);
                _mockDepth.SetValueWithoutNotify(tuning.MockMaxDepthMeters);
            }

            if (_runtime.TryGetLatestTelemetry(out SuitIntegrityTelemetryEntry entry))
            {
                _telemetry.text =
                    $"Frame {entry.Frame} | depth {entry.DepthMeters:0.0} m | pressure {entry.AppliedPressureATM:0.0} atm | over {entry.OverpressureScalar:0.00} | integrity {entry.CurrentIntegrity01:0.00} | {entry.ExecutionMicroseconds:0.0} us";
            }
            else
            {
                _telemetry.text = "Telemetry unavailable";
            }

            _status.text = _runtime.TryGetIntegrity(0, out SuitIntegrityDTO integrity)
                ? $"Suit 0x{integrity.EquippedSuitHash:X8} | fracture {integrity.MicroFractureAccumulation:0.00} | flags 0x{integrity.IntegrityFlags:X8}"
                : "Suit integrity state unavailable";
            _chart.MarkDirtyRepaint();
        }

        private void ApplyTuning()
        {
            if (_runtime == null || !_runtime.TryGetTuning(out SuitIntegrityTuningDTO tuning))
                return;

            tuning.GlobalQualityWeight = math.saturate(_quality.value);
            tuning.WarningOverpressure = math.max(0f, _warning.value);
            tuning.BuckleOverpressure = math.max(tuning.WarningOverpressure, _buckling.value);
            tuning.VisualDeformationGain = math.max(0f, _visualGain.value);
            tuning.MockMaxDepthMeters = math.max(1f, _mockDepth.value);
            _runtime.SetEditorTuning(tuning);
        }

        private void DrawChart(MeshGenerationContext context)
        {
            Rect rect = _chart.contentRect;
            if (rect.width <= 1f || rect.height <= 1f || _runtime == null || !_runtime.TryGetVisual(0, out SuitIntegrityVisualDTO visual))
                return;

            Painter2D painter = context.painter2D;
            DrawRect(painter, new Rect(rect.x, rect.y, rect.width, rect.height), new Color(0.06f, 0.07f, 0.08f, 1f));
            DrawBar(painter, rect, 0, visual.CurrentIntegrity01, new Color(0.15f, 0.75f, 0.50f, 1f));
            DrawBar(painter, rect, 1, math.saturate(visual.OverpressureScalar), new Color(0.95f, 0.55f, 0.12f, 1f));
            DrawBar(painter, rect, 2, visual.Buckling01, new Color(0.90f, 0.10f, 0.08f, 1f));
        }

        private static void DrawBar(Painter2D painter, Rect rect, int index, float value, Color color)
        {
            float width = rect.width / 3f;
            float x = rect.x + index * width + 4f;
            float height = math.saturate(value) * (rect.height - 8f);
            DrawRect(painter, new Rect(x, rect.yMax - height - 4f, width - 8f, height), color);
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

    [InitializeOnLoad]
    internal static class SuitIntegrityDebugGizmo
    {
        static SuitIntegrityDebugGizmo()
        {
            SceneView.duringSceneGui += DrawSceneGui;
        }

        private static void DrawSceneGui(SceneView view)
        {
            if (!Application.isPlaying)
                return;

            ShinobuSuitIntegrityRuntime runtime = Object.FindAnyObjectByType<ShinobuSuitIntegrityRuntime>();
            if (runtime == null || !runtime.TryGetVisual(0, out SuitIntegrityVisualDTO visual))
                return;

            Vector3 center = runtime.transform.position + Vector3.up * 1.4f;
            float radius = 0.35f + math.saturate(visual.Buckling01) * 1.4f;
            Handles.color = Color.Lerp(new Color(0.10f, 0.75f, 0.55f, 1f), new Color(0.90f, 0.08f, 0.04f, 1f), math.saturate(visual.Buckling01));
            Handles.DrawWireCube(center, Vector3.one * radius);
            Handles.Label(center + Vector3.up * radius, "SHINOBU_323 pressure " + visual.AppliedPressureATM.ToString("0.0") + " atm");
        }
    }
}
#endif
