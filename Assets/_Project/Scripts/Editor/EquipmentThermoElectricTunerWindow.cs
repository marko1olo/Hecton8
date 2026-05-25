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
        private const string IlluminationHardwareProfilesCsvPath = "Assets/_Project/Data/Tools/illumination_hardware_profiles.csv";
        private const string LegacyHardwareSpecsCsvPath = "Assets/_Project/Data/Tools/tool_hardware_specs.csv";
        private Slider _baseHeatSlider;
        private Slider _waterCoolingSlider;
        private Slider _coldPenaltySlider;
        private Slider _powerDrawSlider;
        private Toggle _drawGizmosToggle;
        private Label _statusLabel;
        private TelemetryGraphElement _graph;
        private bool _drawGizmos = true;

        [MenuItem("HECTON-8/Tools/Illumination Thermodynamics Tuner")]
        public static void Open()
        {
            GetWindow<EquipmentThermoElectricTunerWindow>("Illumination Thermodynamics");
        }

        private void OnEnable()
        {
            BuildUi();
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
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

            _coldPenaltySlider = new Slider("Cold Battery Penalty", 0f, 4f) { value = 1.85f };
            _coldPenaltySlider.RegisterValueChangedCallback(OnColdPenaltyChanged);
            root.Add(_coldPenaltySlider);

            _powerDrawSlider = new Slider("Power Draw Rate", 0f, 250f) { value = 20f };
            _powerDrawSlider.RegisterValueChangedCallback(OnRateSliderChanged);
            root.Add(_powerDrawSlider);

            _drawGizmosToggle = new Toggle("Draw Live Thermal Gizmo") { value = _drawGizmos };
            _drawGizmosToggle.RegisterValueChangedCallback(OnDrawGizmosChanged);
            root.Add(_drawGizmosToggle);

            Button mockButton = new Button(OnGenerateMockClicked) { text = "Generate Mock Thermal Equipment State" };
            root.Add(mockButton);

            Button loadCsvButton = new Button(OnLoadHardwareSpecsCsvClicked) { text = "Load illumination_hardware_profiles.csv" };
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

            if (engine.TryGetLatestFlashlightTelemetry(out FlashlightTelemetryEntry entry))
            {
                _statusLabel.text = "Frame " + entry.Frame +
                    " | heat " + entry.Thermal01.ToString("0.000") +
                    " | ambient C " + entry.AmbientCelsius.ToString("0.0") +
                    " | depth m " + entry.DepthMeters.ToString("0.0") +
                    " | battery drain Ws " + entry.BatteryDrainWattSeconds.ToString("0.000") +
                    " | Q " + entry.GlobalQualityWeight.ToString("0.00") +
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

        private void OnColdPenaltyChanged(ChangeEvent<float> evt)
        {
            ModularEquipmentEngine engine = ResolveEngine();
            if (engine == null || !engine.TryGetEquipmentTuning(out EquipmentTuningDTO tuning))
                return;

            tuning.ColdBatteryPenaltyMultiplier = math.max(0f, evt.newValue);
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

            string path = File.Exists(IlluminationHardwareProfilesCsvPath)
                ? IlluminationHardwareProfilesCsvPath
                : LegacyHardwareSpecsCsvPath;
            if (!File.Exists(path))
            {
                _statusLabel.text = "Missing " + IlluminationHardwareProfilesCsvPath;
                return;
            }

            byte[] csv = File.ReadAllBytes(path);
            EquipmentCsvParseResult result = engine.IngestToolHardwareSpecsCsv(csv);
            _statusLabel.text = "CSV rows " + result.ParsedRows +
                " | skipped " + result.SkippedRows +
                " | faults 0x" + result.FaultFlags.ToString("X8");
        }

        private void DrawSceneGizmos(SceneView sceneView)
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

            return UnityEngine.Object.FindAnyObjectByType<ModularEquipmentEngine>();
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
                painter.fillColor = new Color(0.14f, 0.52f, 0.9f, 0.42f);
                DrawTelemetryArea(painter, rect, false);
                painter.fillColor = new Color(0.78f, 0.18f, 0.1f, 0.52f);
                DrawTelemetryArea(painter, rect, true);
                painter.lineWidth = 1.5f;
                painter.strokeColor = new Color(0.9f, 0.28f, 0.18f, 0.95f);
                DrawTelemetryLine(painter, rect, true);
                painter.strokeColor = new Color(0.2f, 0.68f, 1f, 0.95f);
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
                    if (!Engine.TryGetFlashlightTelemetryEntry(GraphSamples - 1 - i, out FlashlightTelemetryEntry entry))
                        continue;

                    float value = heat
                        ? math.saturate(entry.Thermal01)
                        : ResolveAmbientCoolingEffect01(entry.AmbientCelsius);
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

            private void DrawTelemetryArea(Painter2D painter, Rect rect, bool heat)
            {
                if (Engine == null || rect.width <= 1f || rect.height <= 1f)
                    return;

                bool started = false;
                float lastX = rect.xMin;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
                for (int i = 0; i < GraphSamples; i++)
                {
                    if (!Engine.TryGetFlashlightTelemetryEntry(GraphSamples - 1 - i, out FlashlightTelemetryEntry entry))
                        continue;

                    float value = heat
                        ? math.saturate(entry.Thermal01)
                        : ResolveAmbientCoolingEffect01(entry.AmbientCelsius);
                    float x = rect.xMin + (rect.width * (i * math.rcp(GraphSamples - 1f)));
                    float y = rect.yMax - (rect.height * value);
                    painter.LineTo(new Vector2(x, y));
                    lastX = x;
                    started = true;
                }

                if (!started)
                    return;

                painter.LineTo(new Vector2(lastX, rect.yMax));
                painter.ClosePath();
                painter.Fill();
            }

            private static float ResolveAmbientCoolingEffect01(float ambientCelsius)
            {
                return math.saturate((22f - ambientCelsius) * 0.041666668f);
            }
        }
    }
}
#endif
