#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Ecosystem;
using Hecton8.World;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class NutrientDriftTunerWindow : EditorWindow
    {
        private const int GraphSamples = NutrientDriftRuntime.TelemetryCapacity;
        private const int SceneSliceSteps = 8;
        private const float SceneCellAlpha = 0.22f;

        private enum TuningField
        {
            CellSize,
            DecayRate,
            InjectionMultiplier,
            AdvectionTimeStep,
            QualityWeight,
            MaxDensity,
            MockWhirlpool
        }

        private Label _stateLabel;
        private Label _headerLabel;
        private Label _telemetryLabel;
        private Slider _cellSizeSlider;
        private Slider _decaySlider;
        private Slider _injectionSlider;
        private Slider _timeStepSlider;
        private Slider _qualitySlider;
        private Slider _maxDensitySlider;
        private Slider _mockFlowSlider;
        private Toggle _drawSliceToggle;
        private Button _reloadProfilesButton;
        private VisualElement _graphElement;
        private bool _refreshing;
        private bool _drawSlice = true;

        [MenuItem("HECTON-8/Ecosystem/Nutrient Drift Tuner")]
        public static void Open()
        {
            GetWindow<NutrientDriftTunerWindow>("Nutrient Drift");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _stateLabel = new Label();
            _stateLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(_stateLabel);

            _headerLabel = new Label();
            root.Add(_headerLabel);

            _cellSizeSlider = CreateSlider("Cell Size Meters", 1f, 64f, TuningField.CellSize);
            _decaySlider = CreateSlider("Decay Per Second", 0f, 0.25f, TuningField.DecayRate);
            _injectionSlider = CreateSlider("Injection Multiplier", 0f, 32f, TuningField.InjectionMultiplier);
            _timeStepSlider = CreateSlider("Advection Step Seconds", 0.05f, 30f, TuningField.AdvectionTimeStep);
            _qualitySlider = CreateSlider("Global Quality Weight", 0f, 1f, TuningField.QualityWeight);
            _maxDensitySlider = CreateSlider("Max Density", 0.1f, 256f, TuningField.MaxDensity);
            _mockFlowSlider = CreateSlider("Mock Flow Speed", 0f, 64f, TuningField.MockWhirlpool);

            root.Add(_cellSizeSlider);
            root.Add(_decaySlider);
            root.Add(_injectionSlider);
            root.Add(_timeStepSlider);
            root.Add(_qualitySlider);
            root.Add(_maxDensitySlider);
            root.Add(_mockFlowSlider);

            _drawSliceToggle = new Toggle("Draw Live Grid Slice") { value = _drawSlice };
            _drawSliceToggle.RegisterValueChangedCallback(evt =>
            {
                _drawSlice = evt.newValue;
                SceneView.RepaintAll();
            });
            root.Add(_drawSliceToggle);

            _reloadProfilesButton = new Button(ReloadProfilesCold) { text = "Reload CSV Profiles" };
            root.Add(_reloadProfilesButton);

            _telemetryLabel = new Label();
            _telemetryLabel.style.marginTop = 6f;
            root.Add(_telemetryLabel);

            _graphElement = new VisualElement();
            _graphElement.style.height = 86f;
            _graphElement.style.marginTop = 6f;
            _graphElement.generateVisualContent += DrawTelemetryGraph;
            root.Add(_graphElement);

            root.schedule.Execute(RefreshFromRuntime).Every(250);
            RefreshFromRuntime();
        }

        private Slider CreateSlider(string label, float min, float max, TuningField field)
        {
            var slider = new Slider(label, min, max) { showInputField = true };
            slider.RegisterValueChangedCallback(evt => SetTuningScalar(field, evt.newValue));
            return slider;
        }

        private void RefreshFromRuntime()
        {
            if (!EditorApplication.isPlaying)
            {
                SetUnavailable("Play Mode required for Vault-backed nutrient drift.");
                return;
            }

            NutrientDriftRuntime.EnsureRuntime();
            if (!NutrientDriftRuntime.TryReadTuning(out NutrientDriftTuningDTO tuning))
            {
                SetUnavailable("Nutrient drift Vault buffers are not registered.");
                return;
            }

            _refreshing = true;
            SetControlsEnabled(true);
            _cellSizeSlider.SetValueWithoutNotify(tuning.CellSizeMeters);
            _decaySlider.SetValueWithoutNotify(tuning.DecayRatePerSecond);
            _injectionSlider.SetValueWithoutNotify(tuning.InjectionMultiplier);
            _timeStepSlider.SetValueWithoutNotify(tuning.AdvectionTimeStep);
            _qualitySlider.SetValueWithoutNotify(tuning.GlobalQualityWeight);
            _maxDensitySlider.SetValueWithoutNotify(tuning.MaxDensity);
            _mockFlowSlider.SetValueWithoutNotify(tuning.MockWhirlpoolMetersPerSecond);
            _refreshing = false;

            _stateLabel.text = "Runtime: linked";
            _headerLabel.text = "Axis " + tuning.ActiveAxis + " / Cells " + tuning.ActiveCellCount + " / Quality " + tuning.GlobalQualityWeight.ToString("0.000");

            if (NutrientDriftRuntime.TryReadGridHeader(out NutrientDriftGridHeaderDTO header))
            {
                _telemetryLabel.text =
                    "Density " + header.TotalDensity.ToString("0.000") +
                    " / Sources " + header.ActiveSources +
                    " / Solver us " + header.LastSolverMicroseconds.ToString("0.0") +
                    " / State 0x" + header.StateHash.ToString("X8");
            }
            else
            {
                _telemetryLabel.text = "Telemetry: unavailable";
            }

            _graphElement?.MarkDirtyRepaint();
        }

        private void SetUnavailable(string message)
        {
            _refreshing = true;
            SetControlsEnabled(false);
            _stateLabel.text = message;
            _headerLabel.text = string.Empty;
            _telemetryLabel.text = string.Empty;
            _refreshing = false;
        }

        private void SetControlsEnabled(bool enabled)
        {
            _cellSizeSlider?.SetEnabled(enabled);
            _decaySlider?.SetEnabled(enabled);
            _injectionSlider?.SetEnabled(enabled);
            _timeStepSlider?.SetEnabled(enabled);
            _qualitySlider?.SetEnabled(enabled);
            _maxDensitySlider?.SetEnabled(enabled);
            _mockFlowSlider?.SetEnabled(enabled);
            _reloadProfilesButton?.SetEnabled(enabled);
        }

        private void ReloadProfilesCold()
        {
            if (!EditorApplication.isPlaying)
                return;

            bool loaded = NutrientDriftRuntime.ForceReloadProfilesCold();
            RefreshFromRuntime();
            _stateLabel.text = loaded ? "CSV profiles reloaded" : "CSV profiles unavailable";
        }

        private void SetTuningScalar(TuningField field, float value)
        {
            if (_refreshing || !EditorApplication.isPlaying)
                return;

            if (!NutrientDriftRuntime.TryReadTuning(out NutrientDriftTuningDTO tuning))
                return;

            switch (field)
            {
                case TuningField.CellSize:
                    tuning.CellSizeMeters = value;
                    break;
                case TuningField.DecayRate:
                    tuning.DecayRatePerSecond = value;
                    break;
                case TuningField.InjectionMultiplier:
                    tuning.InjectionMultiplier = value;
                    break;
                case TuningField.AdvectionTimeStep:
                    tuning.AdvectionTimeStep = value;
                    break;
                case TuningField.QualityWeight:
                    tuning.GlobalQualityWeight = value;
                    break;
                case TuningField.MaxDensity:
                    tuning.MaxDensity = value;
                    break;
                case TuningField.MockWhirlpool:
                    tuning.MockWhirlpoolMetersPerSecond = value;
                    break;
            }

            if (NutrientDriftRuntime.TryWriteTuning(in tuning))
            {
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private void DrawTelemetryGraph(MeshGenerationContext context)
        {
            Rect rect = _graphElement.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Painter2D painter = context.painter2D;
            DrawRect(painter, rect, new Color(0.03f, 0.045f, 0.055f, 1f));

            if (!EditorApplication.isPlaying || !NutrientDriftRuntime.TryReadTelemetryCursor(out int cursor))
                return;

            float maxDensity = 0.0001f;
            for (int i = 0; i < GraphSamples; i++)
            {
                if (NutrientDriftRuntime.TryReadTelemetryEntry(i, out FluidGridTelemetryEntry entry))
                    maxDensity = math.max(maxDensity, entry.TotalDensity);
            }

            painter.lineWidth = 1.5f;
            painter.strokeColor = new Color(0.15f, 0.85f, 0.6f, 1f);
            painter.BeginPath();
            bool hasPoint = false;
            for (int i = 0; i < GraphSamples; i++)
            {
                int index = (cursor + i) % GraphSamples;
                if (!NutrientDriftRuntime.TryReadTelemetryEntry(index, out FluidGridTelemetryEntry entry))
                    continue;

                float x = rect.xMin + (i / (float)(GraphSamples - 1)) * rect.width;
                float y = rect.yMax - math.saturate(entry.TotalDensity / maxDensity) * rect.height;
                var point = new Vector2(x, y);
                if (!hasPoint)
                {
                    painter.MoveTo(point);
                    hasPoint = true;
                }
                else
                {
                    painter.LineTo(point);
                }
            }

            if (hasPoint)
                painter.Stroke();
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

        private void DrawSceneGizmos(SceneView sceneView)
        {
            if (!_drawSlice || !EditorApplication.isPlaying)
                return;

            if (!NutrientDriftRuntime.TryReadGridHeader(out NutrientDriftGridHeaderDTO header) ||
                header.ActiveAxis <= 0 ||
                header.CellSizeMeters <= 0f)
            {
                return;
            }

            int axis = math.clamp(header.ActiveAxis, 1, NutrientDriftRuntime.GridAxisMax);
            int step = math.max(1, axis / SceneSliceSteps);
            int z = axis / 2;
            float half = axis * header.CellSizeMeters * 0.5f;
            AbsoluteUniversePosition runtimeOrigin = GlobalSignals.CurrentRuntimeOriginAup();
            double3 runtimeOriginAup = runtimeOrigin.IsFinite() ? runtimeOrigin.ToAbsoluteDouble3() : double3.zero;
            double3 localOrigin = header.GridOriginAup - runtimeOriginAup;
            Vector3 origin = new Vector3((float)localOrigin.x, (float)localOrigin.y, (float)localOrigin.z);
            Vector3 normal = Vector3.up;

            for (int y = 0; y < axis; y += step)
            {
                for (int x = 0; x < axis; x += step)
                {
                    if (!NutrientDriftRuntime.TryReadDensityCell(x, y, z, out NutrientCellDTO cell))
                        continue;

                    float normalized = math.saturate(cell.Density / math.max(0.0001f, header.TotalDensity * 0.01f));
                    if (normalized <= 0.005f)
                        continue;

                    Vector3 center = origin + new Vector3(
                        (x + 0.5f) * header.CellSizeMeters - half,
                        (y + 0.5f) * header.CellSizeMeters - half,
                        0f);
                    float size = header.CellSizeMeters * step * 0.9f;

                    Color fill = Color.Lerp(new Color(0.02f, 0.22f, 0.18f, SceneCellAlpha), new Color(0.25f, 1f, 0.62f, SceneCellAlpha), normalized);
                    Handles.color = fill;
                    Handles.DrawSolidDisc(center, normal, size * 0.5f);
                    Handles.color = new Color(fill.r, fill.g, fill.b, 0.55f);
                    Handles.DrawWireDisc(center, normal, size * 0.5f);
                    Handles.DrawLine(center, center + normal * math.max(0.25f, normalized * header.CellSizeMeters), 1f);
                }
            }
        }
    }
}
#endif
