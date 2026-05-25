#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - BaseAtmosphereLogisticsEditor.cs
// UI Toolkit tuner and static layout guard for SHINOBU_221.
// ============================================================================

using System.Globalization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Atmosphere.Editor
{
    internal static class BaseAtmosphereLogisticsLayoutValidator
    {
        [InitializeOnLoadMethod]
        private static void Validate()
        {
            if (!AtmosphereLogisticsLayout.ValidateAtmosphereCellLayout() ||
                !AtmosphereLogisticsLayout.ValidateAtmosphereDeltaLaneLayout() ||
                UnsafeUtility.SizeOf<AtmosphereNodeDTO>() != 32 ||
                UnsafeUtility.SizeOf<AtmosphereConnectionDTO>() != 16 ||
                UnsafeUtility.SizeOf<AtmosphereTelemetryEntry>() != 64)
            {
                Debug.LogError("[SHINOBU_221] Base atmosphere logistics layout mismatch.");
            }
        }
    }

    public sealed class BaseAtmosphereLogisticsTunerWindow : EditorWindow
    {
        private float _diffusionRate = 0.35f;
        private float _inhalationMultiplier = 1f;
        private float _toxinDissipation = 0.005f;
        private Slider _diffusionSlider;
        private Slider _inhalationSlider;
        private Slider _toxinSlider;
        private AtmosphereEfficiencyGraphElement _efficiencyGraph;
        private Label _status;
        private bool _suppressCallbacks;

        [MenuItem("HECTON-8/Base Atmosphere Logistics Tuner")]
        public static void Open()
        {
            BaseAtmosphereLogisticsTunerWindow window = GetWindow<BaseAtmosphereLogisticsTunerWindow>();
            window.titleContent = new GUIContent("Base Atmosphere");
            window.minSize = new Vector2(360f, 180f);
            window.RefreshFromRuntime();
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            RefreshFromRuntime();
            PushValues();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _diffusionSlider = CreateSlider("Base Diffusion Rate", 0f, 4f);
            _inhalationSlider = CreateSlider("Inhalation Multiplier", 0f, 4f);
            _toxinSlider = CreateSlider("Toxin Dissipation", 0f, 1f);
            _efficiencyGraph = new AtmosphereEfficiencyGraphElement();
            _status = new Label();
            _status.style.whiteSpace = WhiteSpace.Normal;
            _status.style.marginTop = 8f;

            root.Add(_efficiencyGraph);
            root.Add(_diffusionSlider);
            root.Add(_inhalationSlider);
            root.Add(_toxinSlider);
            root.Add(_status);
            PushValues();
            RefreshStatus();
        }

        private Slider CreateSlider(string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max) { showInputField = true };
            slider.RegisterValueChangedCallback(evt =>
            {
                if (_suppressCallbacks)
                    return;

                if (ReferenceEquals(slider, _diffusionSlider))
                    _diffusionRate = evt.newValue;
                else if (ReferenceEquals(slider, _inhalationSlider))
                    _inhalationMultiplier = evt.newValue;
                else if (ReferenceEquals(slider, _toxinSlider))
                    _toxinDissipation = evt.newValue;

                BaseAtmosphereLogisticsRuntime.SetEditorTuning(_diffusionRate, _inhalationMultiplier, _toxinDissipation);
                RefreshStatus();
            });
            return slider;
        }

        private void OnEditorUpdate()
        {
            _efficiencyGraph?.MarkDirtyRepaint();
            RefreshStatus();
        }

        private void RefreshFromRuntime()
        {
            if (!BaseAtmosphereLogisticsRuntime.TryGetEditorTuning(out AtmosphereTuningDTO tuning))
                return;

            _diffusionRate = tuning.BaseDiffusionRate;
            _inhalationMultiplier = tuning.InhalationMultiplier;
            _toxinDissipation = tuning.ToxinDissipationSpeed;
        }

        private void PushValues()
        {
            _suppressCallbacks = true;
            if (_diffusionSlider != null) _diffusionSlider.SetValueWithoutNotify(_diffusionRate);
            if (_inhalationSlider != null) _inhalationSlider.SetValueWithoutNotify(_inhalationMultiplier);
            if (_toxinSlider != null) _toxinSlider.SetValueWithoutNotify(_toxinDissipation);
            _suppressCallbacks = false;
        }

        private void RefreshStatus()
        {
            if (_status == null)
                return;

            if (BaseAtmosphereLogisticsRuntime.TryGetLatestTelemetry(out AtmosphereTelemetryEntry entry))
            {
                _status.text =
                    "Nodes " + entry.NodeCount +
                    "  Edges " + entry.EdgeCount +
                    "  Iter " + entry.JacobiIterations +
                    "  O2 " + entry.AverageOxygen01.ToString("0.000", CultureInfo.InvariantCulture) +
                    "  CO2max " + entry.MaxCarbonDioxide01.ToString("0.000", CultureInfo.InvariantCulture) +
                    "  Toxin " + entry.MaxToxin01.ToString("0.000", CultureInfo.InvariantCulture);
            }
            else
            {
                _status.text = "No atmosphere telemetry yet.";
            }
        }

        private sealed class AtmosphereEfficiencyGraphElement : VisualElement
        {
            private const float GraphHeightPixels = 112f;
            private const float ColumnPixels = 4f;
            private const float MaxSolverMicroseconds = 1000f;
            private const float MaxCo201 = 0.04f;
            private const float MaxToxin01 = 0.04f;

            public AtmosphereEfficiencyGraphElement()
            {
                style.height = GraphHeightPixels;
                style.marginBottom = 8f;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 1f || rect.height <= 1f)
                    return;

                Painter2D painter = context.painter2D;
                DrawRect(painter, rect, new Color(0.012f, 0.018f, 0.024f, 0.96f));
                if (!BaseAtmosphereLogisticsRuntime.TryGetTelemetryReadOnly(out NativeArray<AtmosphereTelemetryEntry>.ReadOnly telemetry, out int cursor) ||
                    telemetry.Length <= 0)
                {
                    return;
                }

                int columns = math.min(telemetry.Length, math.max(1, (int)math.floor(rect.width / ColumnPixels)));
                float columnWidth = math.max(1f, rect.width / columns);
                int start = cursor - columns;
                for (int i = 0; i < columns; i++)
                {
                    int index = start + i;
                    while (index < 0)
                        index += telemetry.Length;
                    index %= telemetry.Length;

                    AtmosphereTelemetryEntry entry = telemetry[index];
                    if (entry.NodeCount <= 0)
                        continue;

                    float solver01 = math.saturate(entry.SolverMicros / MaxSolverMicroseconds);
                    float oxygenLoss01 = math.saturate((AtmosphereLogisticsConstants.DefaultOxygen01 - entry.AverageOxygen01) / AtmosphereLogisticsConstants.DefaultOxygen01);
                    float co201 = math.saturate(entry.MaxCarbonDioxide01 / MaxCo201);
                    float toxin01 = math.saturate(entry.MaxToxin01 / MaxToxin01);
                    float pressure01 = math.saturate(math.max(solver01, math.max(toxin01, math.max(co201, oxygenLoss01))));
                    float height = math.max(1f, rect.height * pressure01);
                    float x = rect.xMin + i * columnWidth;
                    float y = rect.yMax - height;
                    DrawRect(painter, new Rect(x, y, math.max(1f, columnWidth - 1f), height),
                        ResolvePressureColor(solver01, toxin01, co201, oxygenLoss01));
                }

                float budgetY = rect.yMax - math.saturate(100f / MaxSolverMicroseconds) * rect.height;
                painter.lineWidth = 1f;
                painter.strokeColor = new Color(0.92f, 0.74f, 0.18f, 0.9f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, budgetY));
                painter.LineTo(new Vector2(rect.xMax, budgetY));
                painter.Stroke();
            }

            private static Color ResolvePressureColor(float solver01, float toxin01, float co201, float oxygenLoss01)
            {
                Color cool = new Color(0.08f, 0.44f, 0.92f, 0.82f);
                Color co2 = new Color(0.62f, 0.18f, 0.92f, 0.88f);
                Color toxin = new Color(0.18f, 0.86f, 0.22f, 0.94f);
                Color heat = new Color(1f, 0.18f, 0.06f, 0.96f);
                Color gas = Color.Lerp(cool, co2, math.saturate(math.max(co201, oxygenLoss01)));
                gas = Color.Lerp(gas, toxin, toxin01);
                return Color.Lerp(gas, heat, solver01);
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
}
#endif
