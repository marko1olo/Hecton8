using Hecton8.Habitat.Deformation;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Habitat.Deformation.Editor
{
    public sealed class BaseStructuralWarningTunerWindow : EditorWindow
    {
        private Slider _threshold;
        private Slider _cooldown;
        private Slider _minRadius;
        private Slider _maxRadius;
        private Slider _audioScale;
        private Slider _panicScale;
        private Label _telemetry;
        private TelemetryGraphElement _graph;

        [MenuItem("Hecton8/Habitat/Base Structural Warning Tuner")]
        public static void Open()
        {
            GetWindow<BaseStructuralWarningTunerWindow>("Base Warning");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            _threshold = AddSlider(root, "Stress Threshold", 0.1f, 1f);
            _cooldown = AddSlider(root, "Cooldown Seconds", 0.05f, 8f);
            _minRadius = AddSlider(root, "Min Radius", 0.25f, 25f);
            _maxRadius = AddSlider(root, "Max Radius", 5f, 160f);
            _audioScale = AddSlider(root, "Audio Scale", 0f, 2f);
            _panicScale = AddSlider(root, "Panic Scale", 0f, 2f);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 8;
            Button refresh = new Button(RefreshFromRuntime) { text = "Refresh" };
            Button apply = new Button(ApplyToRuntime) { text = "Apply" };
            Button spike = new Button(GenerateSpike) { text = "Mock Spike" };
            Button csv = new Button(LoadCsv) { text = "Load CSV" };
            row.Add(refresh);
            row.Add(apply);
            row.Add(spike);
            row.Add(csv);
            root.Add(row);

            _telemetry = new Label("No runtime sample.");
            _telemetry.style.marginTop = 10;
            root.Add(_telemetry);

            _graph = new TelemetryGraphElement();
            _graph.style.height = 96;
            _graph.style.marginTop = 8;
            root.Add(_graph);
            RefreshFromRuntime();
        }

        private static Slider AddSlider(VisualElement root, string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high) { showInputField = true };
            slider.style.marginBottom = 4;
            root.Add(slider);
            return slider;
        }

        private void RefreshFromRuntime()
        {
            StructuralIntegrityCalculatorRuntime runtime = StructuralIntegrityCalculatorRuntime.ActiveRuntime;
            if (runtime == null || !runtime.TryGetBaseStructuralWarningTuning(out BaseStructuralWarningTuningDTO tuning))
            {
                _telemetry.text = "Runtime unavailable.";
                return;
            }

            _threshold.SetValueWithoutNotify(tuning.StressThreshold01);
            _cooldown.SetValueWithoutNotify(tuning.CooldownSeconds);
            _minRadius.SetValueWithoutNotify(tuning.MinClusterRadiusMeters);
            _maxRadius.SetValueWithoutNotify(tuning.MaxClusterRadiusMeters);
            _audioScale.SetValueWithoutNotify(tuning.AudioIntensityScale);
            _panicScale.SetValueWithoutNotify(tuning.PanicStressScale);
            UpdateTelemetry(runtime);
        }

        private void ApplyToRuntime()
        {
            StructuralIntegrityCalculatorRuntime runtime = StructuralIntegrityCalculatorRuntime.ActiveRuntime;
            if (runtime == null || !runtime.TryGetBaseStructuralWarningTuning(out BaseStructuralWarningTuningDTO tuning))
                return;

            tuning.StressThreshold01 = math.saturate(_threshold.value);
            tuning.CooldownSeconds = math.max(0.05f, _cooldown.value);
            tuning.MinClusterRadiusMeters = math.max(0.25f, _minRadius.value);
            tuning.MaxClusterRadiusMeters = math.max(tuning.MinClusterRadiusMeters, _maxRadius.value);
            tuning.AudioIntensityScale = math.max(0f, _audioScale.value);
            tuning.PanicStressScale = math.max(0f, _panicScale.value);
            runtime.SetBaseStructuralWarningTuning(in tuning);
            UpdateTelemetry(runtime);
        }

        private void GenerateSpike()
        {
            StructuralIntegrityCalculatorRuntime runtime = StructuralIntegrityCalculatorRuntime.ActiveRuntime;
            if (runtime == null)
                return;

            runtime.GenerateMockStructuralWarningSpike();
            UpdateTelemetry(runtime);
        }

        private void LoadCsv()
        {
            StructuralIntegrityCalculatorRuntime runtime = StructuralIntegrityCalculatorRuntime.ActiveRuntime;
            if (runtime == null)
                return;

            runtime.TryLoadBaseAlarmProfilesCsv();
            UpdateTelemetry(runtime);
        }

        private void UpdateTelemetry(StructuralIntegrityCalculatorRuntime runtime)
        {
            if (!runtime.TryGetBaseStructuralWarningTelemetry(out BaseStructuralWarningTelemetryEntry entry))
            {
                _telemetry.text = "No telemetry entry yet.";
                return;
            }

            _telemetry.text =
                $"Frame {entry.Frame} | raw {entry.RawWarningCount} | groups {entry.GroupedWarningCount} | emitted {entry.EmittedWarningCount} | dropped {entry.DroppedWarningCount} | radius {entry.ClusterRadiusMeters:0.0}m | est {entry.EstimatedMicroseconds:0.0}us | flags 0x{entry.FaultFlags:X8}";
            _graph.Push(entry.HighestStress01, entry.GroupedWarningCount, entry.EstimatedMicroseconds);
        }

        private sealed class TelemetryGraphElement : VisualElement
        {
            private const int Capacity = 128;
            private readonly float[] _stress = new float[Capacity];
            private readonly float[] _groups = new float[Capacity];
            private readonly float[] _micros = new float[Capacity];
            private int _cursor;
            private int _count;

            public TelemetryGraphElement()
            {
                generateVisualContent += Generate;
                style.borderTopWidth = 1;
                style.borderRightWidth = 1;
                style.borderBottomWidth = 1;
                style.borderLeftWidth = 1;
                style.borderTopColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                style.borderRightColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                style.borderLeftColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            }

            public void Push(float stress01, int groupedCount, float estimatedMicroseconds)
            {
                _stress[_cursor] = math.saturate(math.isfinite(stress01) ? stress01 : 0f);
                _groups[_cursor] = math.saturate(groupedCount / 64f);
                _micros[_cursor] = math.saturate((math.isfinite(estimatedMicroseconds) ? estimatedMicroseconds : 0f) / 200f);
                _cursor = (_cursor + 1) & (Capacity - 1);
                if (_count < Capacity)
                    _count++;
                MarkDirtyRepaint();
            }

            private void Generate(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (_count <= 1 || rect.width <= 2f || rect.height <= 2f)
                    return;

                DrawSeries(context, rect, _stress, new Color(1f, 0.18f, 0.08f, 1f));
                DrawSeries(context, rect, _groups, new Color(0.1f, 0.65f, 1f, 1f));
                DrawSeries(context, rect, _micros, new Color(1f, 0.78f, 0.15f, 1f));
            }

            private void DrawSeries(MeshGenerationContext context, Rect rect, float[] values, Color color)
            {
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.35f;
                painter.strokeColor = color;
                painter.BeginPath();
                for (int i = 0; i < _count; i++)
                {
                    int slot = (_cursor - _count + i + Capacity) & (Capacity - 1);
                    float x = rect.xMin + (rect.width * i / math.max(1, _count - 1));
                    float y = rect.yMax - rect.height * math.saturate(values[slot]);
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
