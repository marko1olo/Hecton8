#if UNITY_EDITOR
using Hecton8.Core.Contracts.Physiology;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physiology.Editor
{
    /// <summary>
    /// Editor-only UI Toolkit facade for SHINOBU_320 metabolism tuning. Runtime data remains Vault-owned.
    /// </summary>
    public sealed class PhysiologyMetabolismTunerWindow : EditorWindow
    {
        private Label _runtimeLabel;
        private Label _telemetryLabel;
        private Label _detailTelemetryLabel;
        private Label _stateLabel;
        private VisualElement _burnHeatBar;
        private VisualElement _calorieBurnSegment;
        private VisualElement _heatLossSegment;
        private Slider _calorieDrainScale;
        private Slider _hydrationDrainScale;
        private Slider _temperatureLossRate;
        private Slider _exertionMultiplier;
        private Slider _toxinAccumulation;
        private Slider _qualityWeight;
        private IntegerField _entityRow;
        private ShinobuMetabolismRuntime _runtime;
        private bool _suppressSliderEvents;

        [MenuItem("Hecton8/Physiology/Metabolism Tuner")]
        public static void Open()
        {
            GetWindow<PhysiologyMetabolismTunerWindow>("Metabolism Tuner");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _runtimeLabel = new Label("Runtime: unresolved");
            _telemetryLabel = new Label("Telemetry: no completed tick");
            _detailTelemetryLabel = new Label("Detail: no player row");
            _stateLabel = new Label("State: no row");
            root.Add(_runtimeLabel);
            root.Add(_telemetryLabel);
            root.Add(_detailTelemetryLabel);
            _burnHeatBar = CreateStackedBar(out _calorieBurnSegment, out _heatLossSegment);
            root.Add(_burnHeatBar);
            root.Add(_stateLabel);

            _calorieDrainScale = CreateSlider("Calorie Drain Scale", 0f, 8f);
            _hydrationDrainScale = CreateSlider("Hydration Drain Scale", 0f, 8f);
            _temperatureLossRate = CreateSlider("Temperature Loss Rate", 0f, 0.01f);
            _exertionMultiplier = CreateSlider("Exertion Multiplier", 0f, 1f);
            _toxinAccumulation = CreateSlider("Toxin Accumulation", 0f, 1f);
            _qualityWeight = CreateSlider("Global Quality Weight", 0f, 1f);
            root.Add(_calorieDrainScale);
            root.Add(_hydrationDrainScale);
            root.Add(_temperatureLossRate);
            root.Add(_exertionMultiplier);
            root.Add(_toxinAccumulation);
            root.Add(_qualityWeight);

            _entityRow = new IntegerField("Entity Row");
            _entityRow.value = 0;
            root.Add(_entityRow);

            Button reloadCsv = new Button(ReloadCsv) { text = "Reload CSV Profiles" };
            Button reloadSuitCsv = new Button(ReloadSuitCsv) { text = "Reload Suit CSV Profiles" };
            Button generateMock = new Button(GenerateMock) { text = "Generate Mock Ecosystem" };
            Button dumpBlackBox = new Button(DumpBlackBox) { text = "Dump Black Box" };
            root.Add(reloadCsv);
            root.Add(reloadSuitCsv);
            root.Add(generateMock);
            root.Add(dumpBlackBox);

            RegisterSliders();
            root.schedule.Execute(Refresh).Every(500);
            Refresh();
        }

        private Slider CreateSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            return slider;
        }

        private static VisualElement CreateStackedBar(out VisualElement calorieSegment, out VisualElement heatSegment)
        {
            VisualElement bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.height = 14;
            bar.style.marginTop = 4;
            bar.style.marginBottom = 8;
            bar.style.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);

            calorieSegment = new VisualElement();
            calorieSegment.style.backgroundColor = new Color(0.92f, 0.54f, 0.12f, 1f);
            heatSegment = new VisualElement();
            heatSegment.style.backgroundColor = new Color(0.12f, 0.58f, 0.92f, 1f);
            bar.Add(calorieSegment);
            bar.Add(heatSegment);
            return bar;
        }

        private void RegisterSliders()
        {
            _calorieDrainScale.RegisterValueChangedCallback(_ => ApplySliderState());
            _hydrationDrainScale.RegisterValueChangedCallback(_ => ApplySliderState());
            _temperatureLossRate.RegisterValueChangedCallback(_ => ApplySliderState());
            _exertionMultiplier.RegisterValueChangedCallback(_ => ApplySliderState());
            _toxinAccumulation.RegisterValueChangedCallback(_ => ApplySliderState());
            _qualityWeight.RegisterValueChangedCallback(_ => ApplySliderState());
        }

        private void Refresh()
        {
            _runtime = ResolveRuntime();
            if (_runtime == null)
            {
                _runtimeLabel.text = Application.isPlaying
                    ? "Runtime: ShinobuMetabolismRuntime not found"
                    : "Runtime: enter Play Mode";
                _telemetryLabel.text = "Telemetry: unavailable";
                _detailTelemetryLabel.text = "Detail: unavailable";
                _stateLabel.text = "State: unavailable";
                UpdateStackedBar(0f, 0f);
                return;
            }

            _runtimeLabel.text = "Runtime: " + _runtime.name;
            if (_runtime.TryGetTuning(out MetabolismTuningDTO tuning))
                PushTuningToSliders(tuning);

            if (_runtime.TryGetLatestTelemetry(out MetabolicTelemetryEntry telemetry))
            {
                _telemetryLabel.text =
                    "Frame " + telemetry.Frame +
                    " | Entities " + telemetry.EntityCount +
                    " | AvgCore " + telemetry.AverageCoreTemperature.ToString("0.00") +
                    " | MinCore " + telemetry.MinimumCoreTemperature.ToString("0.00") +
                    " | Toxic " + telemetry.ToxicityCount +
                    " | us " + telemetry.ExecutionMicroseconds.ToString("0.00");
            }

            if (_runtime.TryGetLatestDetailTelemetry(out MetabolicDetailTelemetryEntry detail))
            {
                float heatLossRate = math.abs(detail.ThermalDeltaCelsiusPerSecond);
                _detailTelemetryLabel.text =
                    "Detail | Burn " + detail.ActiveCalorieBurnPerSecond.ToString("0.000") +
                    " | HeatDelta/s " + detail.ThermalDeltaCelsiusPerSecond.ToString("0.000") +
                    " | Ambient " + detail.AmbientCelsius.ToString("0.00") +
                    " | Suit 0x" + detail.SuitProfileHash.ToString("X8");
                UpdateStackedBar(detail.ActiveCalorieBurnPerSecond, heatLossRate);
            }

            int row = math.max(0, _entityRow.value);
            if (_runtime.TryGetState(row, out MetabolicStateDTO state))
            {
                _stateLabel.text =
                    "Row " + row +
                    " | Calories " + state.Calories.ToString("0.00") +
                    " | Hydration " + state.Hydration.ToString("0.00") +
                    " | Core " + state.CoreTemperature.ToString("0.00") +
                    " | Toxicity " + state.Toxicity.ToString("0.000") +
                    " | Flags 0x" + state.Flags.ToString("X8");
            }
        }

        private void UpdateStackedBar(float calorieBurnRate, float heatLossRate)
        {
            float calorie = math.max(0.0001f, math.min(math.abs(calorieBurnRate), 1000f));
            float heat = math.max(0.0001f, math.min(math.abs(heatLossRate), 1000f));
            _calorieBurnSegment.style.flexGrow = calorie;
            _heatLossSegment.style.flexGrow = heat;
        }

        private void PushTuningToSliders(MetabolismTuningDTO tuning)
        {
            _suppressSliderEvents = true;
            _calorieDrainScale.SetValueWithoutNotify(tuning.BaseCalorieDrainScale);
            _hydrationDrainScale.SetValueWithoutNotify(tuning.BaseHydrationDrainScale);
            _temperatureLossRate.SetValueWithoutNotify(tuning.TemperatureLossRate);
            _exertionMultiplier.SetValueWithoutNotify(tuning.ExertionMultiplier);
            _toxinAccumulation.SetValueWithoutNotify(tuning.ToxinAccumulationPerSecond);
            _qualityWeight.SetValueWithoutNotify(tuning.GlobalQualityWeight);
            _suppressSliderEvents = false;
        }

        private void ApplySliderState()
        {
            if (_suppressSliderEvents || _runtime == null || !_runtime.TryGetTuning(out MetabolismTuningDTO tuning))
                return;

            tuning.BaseCalorieDrainScale = _calorieDrainScale.value;
            tuning.BaseHydrationDrainScale = _hydrationDrainScale.value;
            tuning.TemperatureLossRate = _temperatureLossRate.value;
            tuning.ExertionMultiplier = _exertionMultiplier.value;
            tuning.ToxinAccumulationPerSecond = _toxinAccumulation.value;
            tuning.GlobalQualityWeight = _qualityWeight.value;
            _runtime.TrySetTuning(tuning);
        }

        private void ReloadCsv()
        {
            if (_runtime == null)
                _runtime = ResolveRuntime();
            _runtime?.TryLoadBiologicalProfilesCsv();
            Refresh();
        }

        private void ReloadSuitCsv()
        {
            if (_runtime == null)
                _runtime = ResolveRuntime();
            _runtime?.TryLoadSuitThermalProfilesCsv();
            Refresh();
        }

        private void GenerateMock()
        {
            if (_runtime == null)
                _runtime = ResolveRuntime();
            _runtime?.GenerateMockEcosystemMetabolism();
            Refresh();
        }

        private void DumpBlackBox()
        {
            if (_runtime == null)
                _runtime = ResolveRuntime();
            _runtime?.DumpBlackBoxForEditor();
            Refresh();
        }

        private static ShinobuMetabolismRuntime ResolveRuntime()
        {
            return Application.isPlaying
                ? Object.FindAnyObjectByType<ShinobuMetabolismRuntime>()
                : null;
        }
    }
}
#endif
