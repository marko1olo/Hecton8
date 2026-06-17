#if UNITY_EDITOR
using Hecton8.Ecosystem;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class BiomassDecayTunerWindow : EditorWindow
    {
        private BiomassDecayGraphElement _graph;
        private Label _status;
        private Slider _baseDecayRate;
        private Slider _temperatureMultiplier;
        private Slider _scavengerAttractionRadius;
        private bool _updatingControls;

        [MenuItem("Hecton8/Ecosystem/Biomass Decay Tuner")]
        public static void Open()
        {
            GetWindow<BiomassDecayTunerWindow>("Biomass Decay");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _status = new Label("Carrion decay Vault buffers not visible.");
            root.Add(_status);

            _baseDecayRate = CreateSlider("Base Decay Rate", 0f, 0.01f);
            _temperatureMultiplier = CreateSlider("Temperature Multiplier", 0.1f, 8f);
            _scavengerAttractionRadius = CreateSlider("Scavenger Attraction Radius", 1f, 256f);
            root.Add(_baseDecayRate);
            root.Add(_temperatureMultiplier);
            root.Add(_scavengerAttractionRadius);

            Button mockButton = new Button(() =>
            {
                NutrientDriftRuntime.GenerateMockMassExtinctionCold(1024);
                SceneView.RepaintAll();
                Repaint();
            })
            {
                text = "Generate Mock Mass Extinction"
            };
            mockButton.style.marginTop = 6;
            root.Add(mockButton);

            Button reloadButton = new Button(() =>
            {
                NutrientDriftRuntime.ForceReloadCarrionProfilesCold();
                Repaint();
            })
            {
                text = "Reload Carrion Profiles CSV"
            };
            root.Add(reloadButton);

            _graph = new BiomassDecayGraphElement();
            _graph.style.height = 180;
            _graph.style.marginTop = 8;
            root.Add(_graph);

            _baseDecayRate.RegisterValueChangedCallback(evt => MutateTuning(0, evt.newValue));
            _temperatureMultiplier.RegisterValueChangedCallback(evt => MutateTuning(1, evt.newValue));
            _scavengerAttractionRadius.RegisterValueChangedCallback(evt => MutateTuning(2, evt.newValue));

            EditorApplication.update += TickEditor;
            RefreshFromVault();
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickEditor;
        }

        private static Slider CreateSlider(string label, float min, float max)
        {
            return new Slider(label, min, max)
            {
                showInputField = true
            };
        }

        private void TickEditor()
        {
            RefreshFromVault();
            if (_graph != null)
                _graph.MarkDirtyRepaint();
        }

        private void RefreshFromVault()
        {
            if (!NutrientDriftRuntime.TryReadCarrionTuning(out CarrionTuningDTO tuning))
            {
                if (_status != null)
                    _status.text = "Carrion decay runtime has not published a ready tuning snapshot.";
                return;
            }

            _updatingControls = true;
            if (_baseDecayRate != null) _baseDecayRate.SetValueWithoutNotify(tuning.BaseDecayRate);
            if (_temperatureMultiplier != null) _temperatureMultiplier.SetValueWithoutNotify(tuning.HotTemperatureMultiplier);
            if (_scavengerAttractionRadius != null) _scavengerAttractionRadius.SetValueWithoutNotify(tuning.ScavengerAttractionRadius);
            _updatingControls = false;

            if (_status != null)
            {
                CarrionTelemetryEntry latest = FindLatestTelemetry();
                _status.text =
                    "Frame " + latest.Frame +
                    " | Active " + latest.ActiveCarrion +
                    " | Biomass " + latest.ActiveBiomass.ToString("0.0") +
                    " | Injected " + latest.InjectedBiomass.ToString("0.000") +
                    " | Attractors " + latest.AttractionCount +
                    " | Burst us " + latest.BurstExecutionMicroseconds.ToString("0.0");
            }
        }

        private void MutateTuning(int field, float value)
        {
            if (_updatingControls)
                return;

            if (!NutrientDriftRuntime.TryReadCarrionTuning(out CarrionTuningDTO tuning))
                return;

            switch (field)
            {
                case 0:
                    tuning.BaseDecayRate = value;
                    break;
                case 1:
                    tuning.HotTemperatureMultiplier = value;
                    break;
                case 2:
                    tuning.ScavengerAttractionRadius = value;
                    break;
            }

            NutrientDriftRuntime.TryWriteCarrionTuning(tuning);
            SceneView.RepaintAll();
        }

        private static CarrionTelemetryEntry FindLatestTelemetry()
        {
            CarrionTelemetryEntry latest = default;
            if (!NutrientDriftRuntime.TryReadCarrionTelemetryCursor(out int cursor))
                return latest;

            int start = math.max(0, cursor - NutrientDriftRuntime.TelemetryCapacity);
            for (int i = start; i < cursor; i++)
            {
                int index = i % NutrientDriftRuntime.TelemetryCapacity;
                if (NutrientDriftRuntime.TryReadCarrionTelemetryEntry(index, out CarrionTelemetryEntry entry) &&
                    entry.Frame >= latest.Frame)
                {
                    latest = entry;
                }
            }

            return latest;
        }
    }

    internal sealed class BiomassDecayGraphElement : VisualElement
    {
        public BiomassDecayGraphElement()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (rect.width <= 2f || rect.height <= 2f)
                return;

            if (!NutrientDriftRuntime.TryReadCarrionTelemetryCursor(out int cursor))
                return;

            int count = math.min(cursor, NutrientDriftRuntime.TelemetryCapacity);
            if (count <= 1)
                return;

            float maxValue = 1f;
            for (int i = 0; i < count; i++)
            {
                int index = (cursor - count + i) % NutrientDriftRuntime.TelemetryCapacity;
                if (!NutrientDriftRuntime.TryReadCarrionTelemetryEntry(index, out CarrionTelemetryEntry entry))
                    continue;
                maxValue = math.max(maxValue, entry.ActiveBiomass);
                maxValue = math.max(maxValue, entry.InjectedBiomass);
            }

            Painter2D painter = context.painter2D;
            DrawLine(painter, rect, cursor, count, maxValue, new Color(0.12f, 0.82f, 0.28f, 1f), 0);
            DrawLine(painter, rect, cursor, count, maxValue, new Color(0.55f, 0.22f, 0.86f, 1f), 1);
        }

        private static void DrawLine(Painter2D painter, Rect rect, int cursor, int count, float maxValue, Color color, int channel)
        {
            bool started = false;
            painter.strokeColor = color;
            painter.lineWidth = 2f;
            for (int i = 0; i < count; i++)
            {
                int index = (cursor - count + i) % NutrientDriftRuntime.TelemetryCapacity;
                if (!NutrientDriftRuntime.TryReadCarrionTelemetryEntry(index, out CarrionTelemetryEntry entry))
                    continue;

                float raw = channel == 0 ? entry.ActiveBiomass : entry.InjectedBiomass;
                float x = rect.xMin + (count <= 1 ? 0f : (i / (float)(count - 1)) * rect.width);
                float y = rect.yMax - math.saturate(raw / math.max(0.0001f, maxValue)) * rect.height;
                if (!started)
                {
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, y));
                    started = true;
                }
                else
                {
                    painter.LineTo(new Vector2(x, y));
                }
            }

            if (started)
                painter.Stroke();
        }
    }
}
#endif
