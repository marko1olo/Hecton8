#if UNITY_EDITOR
using Hecton8.Habitat.Deformation;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Habitat.Deformation.Editor
{
    public sealed class StructuralIntegrityTunerWindow : EditorWindow
    {
        private Slider _basePressure;
        private Slider _pressureGradient;
        private Slider _materialStrength;
        private Slider _buckling;
        private Slider _support;
        private Slider _collapse;
        private Slider _quality;
        private Label _status;
        private StressGraphElement _graph;
        private int _suppressWrite;
        private double _nextStatusRefreshTime;
        private int _lastStatusNodeCount = -1;
        private int _lastStatusQualityMilli = -1;

        [MenuItem("Hecton8/Habitat/Structural Integrity Calculator")]
        public static void Open()
        {
            StructuralIntegrityTunerWindow window = GetWindow<StructuralIntegrityTunerWindow>();
            window.titleContent = new GUIContent("Hull Integrity Tuner");
            window.minSize = new Vector2(420f, 320f);
        }

        [MenuItem("Hecton8/Habitat/Hull Integrity Tuner")]
        public static void OpenHullIntegrityTuner()
        {
            Open();
        }

        private void OnEnable()
        {
            EditorApplication.update -= RefreshFromRuntime;
            EditorApplication.update += RefreshFromRuntime;
            SceneView.duringSceneGui -= DrawSceneHeatmap;
            SceneView.duringSceneGui += DrawSceneHeatmap;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshFromRuntime;
            SceneView.duringSceneGui -= DrawSceneHeatmap;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;

            _status = new Label("Runtime not bound");
            root.Add(_status);

            _basePressure = CreateSlider("Base Pressure kPa", 0f, 400f);
            _pressureGradient = CreateSlider("Depth Pressure kPa/m", 0f, 25f);
            _materialStrength = CreateSlider("Material Strength Factor", 0.1f, 8f);
            _buckling = CreateSlider("Buckling Visual Intensity", 0f, 4f);
            _support = CreateSlider("Support Damping", 0f, 2f);
            _collapse = CreateSlider("Collapse Stress", 0.1f, 2f);
            _quality = CreateSlider("Authoritative Quality Weight", 0f, 1f);

            root.Add(_basePressure);
            root.Add(_pressureGradient);
            root.Add(_materialStrength);
            root.Add(_buckling);
            root.Add(_support);
            root.Add(_collapse);
            root.Add(_quality);

            Button regenerate = new Button(RegenerateMockGraph) { text = "Regenerate Mock Graph" };
            root.Add(regenerate);

            _graph = new StressGraphElement();
            _graph.style.height = 112f;
            _graph.style.marginTop = 8f;
            root.Add(_graph);
            RefreshFromRuntime();
        }

        private Slider CreateSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(_ => WriteTuning());
            return slider;
        }

        private void RefreshFromRuntime()
        {
            StructuralIntegrityCalculatorRuntime runtime = StructuralIntegrityCalculatorRuntime.ActiveRuntime;
            if (runtime == null || !runtime.TryGetTuning(out StructuralTuningDTO tuning))
            {
                if (_status != null)
                {
                    _status.text = "Runtime not bound";
                    _lastStatusNodeCount = -1;
                    _lastStatusQualityMilli = -1;
                }

                return;
            }

            double now = EditorApplication.timeSinceStartup;
            int nodeCount = runtime.ActiveNodeCount;
            int qualityMilli = Mathf.RoundToInt(Mathf.Clamp01(tuning.GlobalQualityWeight) * 1000f);
            if (_status != null && now >= _nextStatusRefreshTime &&
                (nodeCount != _lastStatusNodeCount || qualityMilli != _lastStatusQualityMilli))
            {
                _nextStatusRefreshTime = now + 0.25d;
                _lastStatusNodeCount = nodeCount;
                _lastStatusQualityMilli = qualityMilli;
                int whole = qualityMilli / 1000;
                int fraction = qualityMilli - whole * 1000;
                char hundreds = (char)('0' + (fraction / 100));
                char tens = (char)('0' + ((fraction / 10) % 10));
                char ones = (char)('0' + (fraction % 10));
                _status.text = "Runtime bound | Nodes " + nodeCount + " | Quality " + whole + "." + hundreds + tens + ones;
            }

            _suppressWrite = 1;
            SetSliderWithoutNotify(_basePressure, tuning.BasePressureKPa);
            SetSliderWithoutNotify(_pressureGradient, tuning.PressureGradientKPaPerMeter);
            SetSliderWithoutNotify(_materialStrength, tuning.MaterialStrengthFactor);
            SetSliderWithoutNotify(_buckling, tuning.BucklingVisualIntensity);
            SetSliderWithoutNotify(_support, tuning.SupportDamping);
            SetSliderWithoutNotify(_collapse, tuning.CollapseStress01);
            SetSliderWithoutNotify(_quality, tuning.GlobalQualityWeight);
            _suppressWrite = 0;

            if (_graph != null)
            {
                _graph.Runtime = runtime;
                _graph.MarkDirtyRepaint();
            }
        }

        private void WriteTuning()
        {
            if (_suppressWrite != 0)
                return;

            StructuralIntegrityCalculatorRuntime runtime = StructuralIntegrityCalculatorRuntime.ActiveRuntime;
            if (runtime == null || !runtime.TryGetTuning(out StructuralTuningDTO tuning))
                return;

            tuning.BasePressureKPa = _basePressure != null ? _basePressure.value : tuning.BasePressureKPa;
            tuning.PressureGradientKPaPerMeter = _pressureGradient != null ? _pressureGradient.value : tuning.PressureGradientKPaPerMeter;
            tuning.MaterialStrengthFactor = _materialStrength != null ? _materialStrength.value : tuning.MaterialStrengthFactor;
            tuning.BucklingVisualIntensity = _buckling != null ? _buckling.value : tuning.BucklingVisualIntensity;
            tuning.SupportDamping = _support != null ? _support.value : tuning.SupportDamping;
            tuning.CollapseStress01 = _collapse != null ? _collapse.value : tuning.CollapseStress01;
            tuning.GlobalQualityWeight = _quality != null ? _quality.value : tuning.GlobalQualityWeight;
            runtime.SetTuning(in tuning);
        }

        private void RegenerateMockGraph()
        {
            StructuralIntegrityCalculatorRuntime runtime = StructuralIntegrityCalculatorRuntime.ActiveRuntime;
            if (runtime == null)
            {
                if (_status != null)
                    _status.text = "Runtime not bound";
                return;
            }

            bool regenerated = runtime.RegenerateMockGraph();
            if (_status != null)
                _status.text = regenerated ? "Mock graph regenerated" : "Mock graph busy or locked";
        }

        private static void DrawSceneHeatmap(SceneView sceneView)
        {
            StructuralIntegrityCalculatorRuntime runtime = StructuralIntegrityCalculatorRuntime.ActiveRuntime;
            if (runtime == null)
                return;

            if (!runtime.TryGetTuning(out StructuralTuningDTO tuning))
                return;

            int count = Mathf.Min(runtime.ActiveNodeCount, 512);
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            for (int i = 0; i < count; i++)
            {
                if (!runtime.TryGetState(i, out IntegrityStateDTO state, out double3 aup))
                    continue;

                float stress = Mathf.Clamp01(state.CurrentStress);
                Color color = Color.Lerp(Color.green, Color.yellow, Mathf.Clamp01(stress / 0.8f));
                if (stress >= 0.95f)
                {
                    float pulse = Mathf.PingPong((float)EditorApplication.timeSinceStartup * 4f, 1f);
                    color = Color.Lerp(Color.red, Color.white, pulse * 0.35f);
                }

                if (!StructuralIntegrityCalculatorRuntime.TryBuildEditorRelativePosition(aup, tuning.SeaLevelAup, out Vector3 position))
                    continue;

                float size = Mathf.Lerp(0.18f, 0.85f, stress);
                Handles.color = color;
                Handles.DrawWireCube(position, Vector3.one * size);
            }
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        private sealed class StressGraphElement : VisualElement
        {
            public StructuralIntegrityCalculatorRuntime Runtime;

            public StressGraphElement()
            {
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect r = contentRect;
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.5f;
                painter.strokeColor = new Color(0.18f, 0.18f, 0.18f, 1f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(r.xMin, r.yMax - 1f));
                painter.LineTo(new Vector2(r.xMax, r.yMax - 1f));
                painter.Stroke();

                if (Runtime == null)
                    return;

                int samples = Mathf.Min(StructuralIntegrityConstants.TelemetryFrameCapacity, 128);
                bool hasPoint = false;
                painter.strokeColor = new Color(0.9f, 0.18f, 0.1f, 1f);
                painter.lineWidth = 2f;
                painter.BeginPath();
                for (int i = 0; i < samples; i++)
                {
                    int framesBack = samples - 1 - i;
                    if (!Runtime.TryGetTelemetrySample(framesBack, out StructuralTelemetryEntry entry))
                        continue;

                    float x = samples <= 1 ? r.xMin : Mathf.Lerp(r.xMin, r.xMax, i / (float)(samples - 1));
                    float y = Mathf.Lerp(r.yMax - 4f, r.yMin + 4f, Mathf.Clamp01(entry.MaxStress01));
                    if (!hasPoint)
                    {
                        painter.MoveTo(new Vector2(x, y));
                        hasPoint = true;
                    }
                    else
                    {
                        painter.LineTo(new Vector2(x, y));
                    }
                }

                if (hasPoint)
                    painter.Stroke();
            }
        }
    }
}
#endif
