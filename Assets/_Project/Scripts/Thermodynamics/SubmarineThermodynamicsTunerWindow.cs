#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Thermodynamics
{
    public sealed class SubmarineThermodynamicsTunerWindow : EditorWindow
    {
        private Slider _qualityPreview;
        private Slider _baseDissipation;
        private Slider _forcedConvection;
        private Slider _safeTemp;
        private Slider _meltdownTemp;
        private FloatField _heatCapacity;
        private Label _status;
        private ReactorTelemetryGraphElement _graph;

        [MenuItem("Hecton8/Thermodynamics/Submarine Thermodynamics Tuner")]
        public static void Open()
        {
            GetWindow<SubmarineThermodynamicsTunerWindow>("Submarine Thermodynamics");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;
            rootVisualElement.style.marginBottom = 6;

            _status = new Label("Runtime unavailable");
            rootVisualElement.Add(_status);

            _qualityPreview = new Slider("Quality kernel preview", 0f, 1f);
            _qualityPreview.SetEnabled(false);
            rootVisualElement.Add(_qualityPreview);

            _baseDissipation = AddSlider("Base dissipation rate", 0f, 0.3f);
            _forcedConvection = AddSlider("Forced convection", 0f, 0.25f);
            _safeTemp = AddSlider("Safe core temp C", 300f, 1200f);
            _meltdownTemp = AddSlider("Meltdown core temp C", 900f, 2600f);
            _heatCapacity = AddFloat("Core heat capacity J/C");

            Button apply = new Button(Apply) { text = "Apply Reactor Tuning" };
            rootVisualElement.Add(apply);

            Button refresh = new Button(Refresh) { text = "Refresh Runtime Values" };
            rootVisualElement.Add(refresh);

            _graph = new ReactorTelemetryGraphElement();
            _graph.style.height = 140;
            _graph.style.marginTop = 8;
            rootVisualElement.Add(_graph);

            Refresh();
            EditorApplication.update -= RepaintGraph;
            EditorApplication.update += RepaintGraph;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintGraph;
        }

        private FloatField AddFloat(string label)
        {
            FloatField field = new FloatField(label);
            rootVisualElement.Add(field);
            return field;
        }

        private Slider AddSlider(string label, float lowValue, float highValue)
        {
            Slider slider = new Slider(label, lowValue, highValue);
            slider.showInputField = true;
            rootVisualElement.Add(slider);
            return slider;
        }

        private void Refresh()
        {
            AbyssalThermodynamicsSolver runtime = AbyssalThermodynamicsSolver.ActiveRuntimeInstance;
            if (runtime == null || !runtime.TryReadReactorTuning(out ReactorThermalTuningDTO tuning))
            {
                _status.text = "Runtime unavailable";
                return;
            }

            _status.text = runtime.TryReadReactorTelemetry(0, out ReactorThermalTelemetryEntry entry)
                ? $"Frame {entry.Frame} | Reactors {entry.ActiveReactorCount} | Heat {entry.TotalJoulesInjected:0} J | Cost {entry.LastInjectionMicroseconds:0.0} us"
                : "Runtime active | no reactor telemetry yet";
            _qualityPreview.SetValueWithoutNotify(tuning.GlobalQualityWeight);
            _baseDissipation.SetValueWithoutNotify(tuning.BaseDissipationRate);
            _forcedConvection.SetValueWithoutNotify(tuning.ForcedConvectionMultiplier);
            _safeTemp.SetValueWithoutNotify(tuning.SafeCoreTempCelsius);
            _meltdownTemp.SetValueWithoutNotify(tuning.MeltdownCoreTempCelsius);
            _heatCapacity.SetValueWithoutNotify(tuning.CoreHeatCapacityJoulesPerCelsius);
        }

        private void Apply()
        {
            AbyssalThermodynamicsSolver runtime = AbyssalThermodynamicsSolver.ActiveRuntimeInstance;
            if (runtime == null || !runtime.TryReadReactorTuning(out ReactorThermalTuningDTO tuning))
            {
                _status.text = "Runtime unavailable";
                return;
            }

            tuning.BaseDissipationRate = Mathf.Max(0f, _baseDissipation.value);
            tuning.ForcedConvectionMultiplier = Mathf.Max(0f, _forcedConvection.value);
            tuning.SafeCoreTempCelsius = Mathf.Max(1f, _safeTemp.value);
            tuning.MeltdownCoreTempCelsius = Mathf.Max(tuning.SafeCoreTempCelsius + 1f, _meltdownTemp.value);
            tuning.CoreHeatCapacityJoulesPerCelsius = Mathf.Max(1f, _heatCapacity.value);
            _status.text = runtime.TryWriteReactorTuning(tuning) ? "Applied" : "Write rejected: solver job pending";
        }

        private void RepaintGraph()
        {
            if (_graph != null)
                _graph.MarkDirtyRepaint();
            Refresh();
        }

        private sealed class ReactorTelemetryGraphElement : VisualElement
        {
            private const int Samples = 80;

            public ReactorTelemetryGraphElement()
            {
                generateVisualContent += DrawGraph;
            }

            private void DrawGraph(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 2f || rect.height <= 2f)
                    return;

                Painter2D painter = context.painter2D;
                FillRect(painter, rect, new Color(0.035f, 0.04f, 0.045f, 1f));
                DrawHorizontalGuide(painter, rect, 0.25f, new Color(1f, 1f, 1f, 0.08f));
                DrawHorizontalGuide(painter, rect, 0.50f, new Color(1f, 1f, 1f, 0.11f));
                DrawHorizontalGuide(painter, rect, 0.75f, new Color(1f, 1f, 1f, 0.08f));

                AbyssalThermodynamicsSolver runtime = AbyssalThermodynamicsSolver.ActiveRuntimeInstance;
                if (runtime == null)
                    return;

                Vector2 previousHeat = default;
                Vector2 previousCore = default;
                bool hasPrevious = false;
                for (int i = Samples - 1; i >= 0; i--)
                {
                    if (!runtime.TryReadReactorTelemetry(i, out ReactorThermalTelemetryEntry entry))
                        continue;

                    float x = Mathf.Lerp(rect.xMin + 4f, rect.xMax - 4f, 1f - i / (float)(Samples - 1));
                    float heatY = Mathf.Lerp(rect.yMax - 6f, rect.yMin + 6f, Mathf.Clamp01(entry.TotalJoulesInjected / 2500000f));
                    float coreY = Mathf.Lerp(rect.yMax - 6f, rect.yMin + 6f, Mathf.Clamp01(entry.MaxCoreTempCelsius / 2000f));
                    Vector2 heat = new Vector2(x, heatY);
                    Vector2 core = new Vector2(x, coreY);
                    if (hasPrevious)
                    {
                        DrawSegment(painter, previousHeat, heat, Color.cyan, 1.35f);
                        DrawSegment(painter, previousCore, core, Color.red, 1.35f);
                    }

                    previousHeat = heat;
                    previousCore = core;
                    hasPrevious = true;
                }
            }

            private static void FillRect(Painter2D painter, Rect rect, Color color)
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

            private static void DrawHorizontalGuide(Painter2D painter, Rect rect, float normalizedHeight, Color color)
            {
                float y = Mathf.Lerp(rect.yMax - 6f, rect.yMin + 6f, Mathf.Clamp01(normalizedHeight));
                DrawSegment(painter, new Vector2(rect.xMin + 4f, y), new Vector2(rect.xMax - 4f, y), color, 1f);
            }

            private static void DrawSegment(Painter2D painter, Vector2 from, Vector2 to, Color color, float width)
            {
                painter.strokeColor = color;
                painter.lineWidth = width;
                painter.BeginPath();
                painter.MoveTo(from);
                painter.LineTo(to);
                painter.Stroke();
            }
        }
    }
}
#endif
