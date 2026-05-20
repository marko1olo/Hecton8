#if UNITY_EDITOR
namespace Hecton8.EditorTools
{
    using Hecton8.Core;
    using Hecton8.Tools;
    using System.IO;
    using Unity.Mathematics;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public sealed class EquipmentThermoElectricTunerWindow : EditorWindow
    {
        private const int GraphSamples = 120;
        private const string HardwareSpecsCsvPath = "Assets/_Project/Data/Tools/tool_hardware_specs.csv";
        private Slider _baseHeatSlider;
        private Slider _waterCoolingSlider;
        private Slider _powerDrawSlider;
        private Toggle _drawGizmosToggle;
        private Label _statusLabel;
        private TelemetryGraphElement _graph;
        private bool _drawGizmos = true;

        [MenuItem("HECTON-8/Tools/Tool Cargo-Electric Tuner")]
        public static void Open()
        {
            GetWindow<EquipmentThermoElectricTunerWindow>("Tool Cargo-Electric");
        }

        private void OnEnable()
        {
            BuildUi();
            SceneView.duringSceneGui -= OnDrawGizmos;
            SceneView.duringSceneGui += OnDrawGizmos;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnDrawGizmos;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void BuildUi()
        {
            VisualElement root = rootVisualElement;
            root.Clear();

            _statusLabel = new Label("Runtime not registered.");
            root.Add(_statusLabel);

            _baseHeatSlider = new Slider("Base Heat Generation", 0f, 2.5f) { value = 0.2f };
            _baseHeatSlider.RegisterValueChangedCallback(OnRateSliderChanged);
            root.Add(_baseHeatSlider);

            _waterCoolingSlider = new Slider("Water Cooling Multiplier", 1f, 8f) { value = 2.75f };
            _waterCoolingSlider.RegisterValueChangedCallback(OnWaterCoolingChanged);
            root.Add(_waterCoolingSlider);

            _powerDrawSlider = new Slider("Power Draw Rate", 0f, 250f) { value = 20f };
            _powerDrawSlider.RegisterValueChangedCallback(OnRateSliderChanged);
            root.Add(_powerDrawSlider);

            _drawGizmosToggle = new Toggle("Draw Live Thermal Gizmo") { value = _drawGizmos };
            _drawGizmosToggle.RegisterValueChangedCallback(OnDrawGizmosChanged);
            root.Add(_drawGizmosToggle);

            Button mockButton = new Button(OnGenerateMockClicked) { text = "Generate Mock Equipment State" };
            root.Add(mockButton);

            Button loadCsvButton = new Button(OnLoadHardwareSpecsCsvClicked) { text = "Load tool_hardware_specs.csv" };
            root.Add(loadCsvButton);

            _graph = new TelemetryGraphElement();
            _graph.style.height = 180f;
            _graph.style.marginTop = 8f;
            root.Add(_graph);
        }

        private void OnEditorUpdate()
        {
            ModularEquipmentEngine engine = ResolveEngine();
            if (engine == null)
            {
                _statusLabel.text = "Runtime not registered.";
                return;
            }

            if (engine.TryGetLatestEquipmentTelemetry(out EquipmentTelemetryEntry entry))
            {
                _statusLabel.text = "Tick " + entry.TickIndex +
                    " | peak heat " + entry.PeakThermal01.ToString("0.000") +
                    " | battery drain Ws " + entry.BatteryDrainWattSeconds.ToString("0.000") +
                    " | Burst us " + entry.CpuMicroseconds.ToString("0.0");
            }
            else
            {
                _statusLabel.text = "Runtime registered. Waiting for equipment telemetry.";
            }

            _graph.Engine = engine;
            _graph.MarkDirtyRepaint();
        }

        private void OnRateSliderChanged(ChangeEvent<float> evt)
        {
            ModularEquipmentEngine engine = ResolveEngine();
            if (engine == null)
                return;

            engine.SetEquipmentSlotRatesForEditor(0, _powerDrawSlider.value, _baseHeatSlider.value);
        }

        private void OnWaterCoolingChanged(ChangeEvent<float> evt)
        {
            ModularEquipmentEngine engine = ResolveEngine();
            if (engine == null || !engine.TryGetEquipmentTuning(out EquipmentTuningDTO tuning))
                return;

            tuning.WaterCoolingMultiplier = math.max(1f, evt.newValue);
            engine.SetEquipmentTuning(in tuning);
        }

        private void OnDrawGizmosChanged(ChangeEvent<bool> evt)
        {
            _drawGizmos = evt.newValue;
        }

        private void OnGenerateMockClicked()
        {
            ModularEquipmentEngine engine = ResolveEngine();
            if (engine != null)
                engine.GenerateMockEquipmentState();
        }

        private void OnLoadHardwareSpecsCsvClicked()
        {
            ModularEquipmentEngine engine = ResolveEngine();
            if (engine == null)
            {
                _statusLabel.text = "Runtime not registered.";
                return;
            }

            if (!File.Exists(HardwareSpecsCsvPath))
            {
                _statusLabel.text = "Missing " + HardwareSpecsCsvPath;
                return;
            }

            byte[] csv = File.ReadAllBytes(HardwareSpecsCsvPath);
            EquipmentCsvParseResult result = engine.IngestToolHardwareSpecsCsv(csv);
            _statusLabel.text = "CSV rows " + result.ParsedRows +
                " | skipped " + result.SkippedRows +
                " | faults 0x" + result.FaultFlags.ToString("X8");
        }

        private void OnDrawGizmos(SceneView sceneView)
        {
            if (!_drawGizmos)
                return;

            ModularEquipmentEngine engine = ResolveEngine();
            if (engine == null)
                return;

            Vector3 root = ResolvePlayerPosition(engine);
            for (int i = 0; i < 16; i++)
            {
                if (!engine.TryGetActiveEquipmentSlot(i, out ActiveEquipmentDTO state))
                    continue;

                float heat = math.saturate(state.ThermalLoad);
                Vector3 position = root + (Vector3.up * (1.2f + (i * 0.12f))) + (Vector3.right * (i * 0.08f));
                Handles.color = Color.Lerp(Color.blue, Color.red, heat);
                Handles.DrawWireDisc(position, Vector3.up, 0.12f + (heat * 0.1f));
                Handles.Label(position + Vector3.up * 0.08f, "0x" + state.ToolHashID.ToString("X8") +
                    " B " + state.CurrentBattery.ToString("0.0") +
                    " H " + state.ThermalLoad.ToString("0.000"));
            }
        }

        private static Vector3 ResolvePlayerPosition(ModularEquipmentEngine engine)
        {
            IPlayerRuntimeContext player = GlobalRegistry.Player;
            if (player != null && player.PlayerTransform != null)
                return player.PlayerTransform.position;

            return engine.transform.position;
        }

        private static ModularEquipmentEngine ResolveEngine()
        {
            IModularEquipmentService service = GlobalRegistry.ModularEquipment;
            if (service is ModularEquipmentEngine engine)
                return engine;

            return UnityEngine.Object.FindObjectOfType<ModularEquipmentEngine>();
        }

        private sealed class TelemetryGraphElement : VisualElement
        {
            public ModularEquipmentEngine Engine;

            public TelemetryGraphElement()
            {
                generateVisualContent += GenerateGraph;
            }

            private void GenerateGraph(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                painter.lineWidth = 2f;
                painter.strokeColor = new Color(0.75f, 0.15f, 0.12f, 1f);
                DrawTelemetryLine(painter, rect, true);
                painter.strokeColor = new Color(0.15f, 0.55f, 0.9f, 1f);
                DrawTelemetryLine(painter, rect, false);
            }

            private void DrawTelemetryLine(Painter2D painter, Rect rect, bool heat)
            {
                if (Engine == null || rect.width <= 1f || rect.height <= 1f)
                    return;

                bool started = false;
                painter.BeginPath();
                for (int i = 0; i < GraphSamples; i++)
                {
                    if (!Engine.TryGetEquipmentTelemetryEntry(GraphSamples - 1 - i, out EquipmentTelemetryEntry entry))
                        continue;

                    float value = heat
                        ? math.saturate(entry.PeakThermal01)
                        : math.saturate(entry.BatteryDrainWattSeconds * 0.02f);
                    float x = rect.xMin + (rect.width * (i * math.rcp(GraphSamples - 1f)));
                    float y = rect.yMax - (rect.height * value);
                    if (!started)
                    {
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
}
#endif
