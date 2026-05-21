#if UNITY_EDITOR
using Hecton8.Core.Contracts.Physiology;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physiology.Editor
{
    /// <summary>
    /// Editor-only UI Toolkit facade for SHINOBU_145 metabolism tuning. Runtime data remains Vault-owned.
    /// </summary>
    public sealed class PhysiologyMetabolismTunerWindow : EditorWindow
    {
        private Label _runtimeLabel;
        private Label _telemetryLabel;
        private Label _stateLabel;
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
            _stateLabel = new Label("State: no row");
            root.Add(_runtimeLabel);
            root.Add(_telemetryLabel);
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
            Button generateMock = new Button(GenerateMock) { text = "Generate Mock Ecosystem" };
            Button dumpBlackBox = new Button(DumpBlackBox) { text = "Dump Black Box" };
            root.Add(reloadCsv);
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
                _stateLabel.text = "State: unavailable";
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
            _runtime?.TryDumpBlackBoxForEditor();
            Refresh();
        }

        private static ShinobuMetabolismRuntime ResolveRuntime()
        {
            return Application.isPlaying
                ? Object.FindFirstObjectByType<ShinobuMetabolismRuntime>()
                : null;
        }
    }
}
#endif
