#if UNITY_EDITOR
using System.IO;
using Hecton8.Power;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed unsafe class SubmarineOsTunerWindow : EditorWindow
    {
        private Label _status;
        private SubmarineOsThermalGridRuntime _runtime;
        private SubmarineThermalGridTuningDTO* _tuning;
        private bool _ownsRuntime;

        [MenuItem("Hecton8/Submarine/Submarine OS Tuner")]
        public static void Open()
        {
            SubmarineOsTunerWindow window = GetWindow<SubmarineOsTunerWindow>("Submarine OS Tuner");
            window.minSize = new Vector2(380f, 320f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            _status = new Label("Vault unavailable");
            rootVisualElement.Add(_status);

            AddSlider("Base Resistance", 0.001f, 1f, value => _tuning->BaseResistance = value, () => _tuning->BaseResistance);
            AddSlider("Thermal Dissipation Rate", 0f, 2f, value => _tuning->ThermalDissipationRate = value, () => _tuning->ThermalDissipationRate);
            AddSlider("Jacobi Tolerance", 0.0001f, 0.05f, value => _tuning->JacobiTolerance = value, () => _tuning->JacobiTolerance);
            AddSlider("Damage Threshold", 0.05f, 4f, value => _tuning->DamageThreshold = value, () => _tuning->DamageThreshold);
            AddSlider("Critical Threshold", 0.1f, 8f, value => _tuning->CriticalThermalThreshold = value, () => _tuning->CriticalThermalThreshold);
            AddSlider("External Heat Scale", 0f, 2f, value => _tuning->ExternalHeatScale = value, () => _tuning->ExternalHeatScale);
            AddSlider("Visual Overkill", 0f, 1f, value => _tuning->VisualOverkillScalar = value, () => _tuning->VisualOverkillScalar);

            Button reloadCsv = new Button(ReloadCsv) { text = "Reload submarine_grid_specs.csv" };
            rootVisualElement.Add(reloadCsv);
            RefreshRuntime();
        }

        private void OnFocus()
        {
            RefreshRuntime();
        }

        private void OnDisable()
        {
            if (!_ownsRuntime)
                return;

            _runtime?.Dispose();
            _runtime = null;
            _tuning = null;
            _ownsRuntime = false;
        }

        private void RefreshRuntime()
        {
            SubmarineOsThermalGridRuntime active = SubmarineOsThermalGridRuntime.Active;
            if (active != null)
            {
                if (_ownsRuntime && !ReferenceEquals(_runtime, active))
                    _runtime?.Dispose();
                _runtime = active;
                _ownsRuntime = false;
            }
            else if (_runtime == null || !_ownsRuntime)
            {
                _runtime = new SubmarineOsThermalGridRuntime();
                _ownsRuntime = true;
            }

            if (!_runtime.EnsureInitialized() || !_runtime.TryGetTuningPointer(out _tuning) || _tuning == null)
            {
                _status.text = "GlobalDataVault unavailable";
                SetControlsEnabled(false);
                return;
            }

            _status.text = "Vault tuning DTO: live";
            SetControlsEnabled(true);
            RefreshSliders();
        }

        private void AddSlider(string label, float min, float max, System.Action<float> setter, System.Func<float> getter)
        {
            Slider slider = new Slider(label, min, max) { showInputField = true };
            slider.userData = getter;
            slider.RegisterValueChangedCallback(evt =>
            {
                if (_tuning == null)
                    return;
                setter(evt.newValue);
            });
            rootVisualElement.Add(slider);
        }

        private void RefreshSliders()
        {
            UQueryBuilder<Slider> query = rootVisualElement.Query<Slider>();
            query.ForEach(slider =>
            {
                if (slider.userData is System.Func<float> getter)
                    slider.SetValueWithoutNotify(getter());
            });
        }

        private void SetControlsEnabled(bool enabled)
        {
            UQueryBuilder<VisualElement> query = rootVisualElement.Query<VisualElement>();
            query.ForEach(element =>
            {
                if (!ReferenceEquals(element, _status))
                    element.SetEnabled(enabled);
            });
        }

        private void ReloadCsv()
        {
            RefreshRuntime();
            if (_runtime == null || _tuning == null)
                return;

            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string path = Path.Combine(root, "StreamingAssets", "submarine_grid_specs.csv");
            if (!File.Exists(path))
                path = Path.Combine(Application.streamingAssetsPath, "submarine_grid_specs.csv");

            bool loaded = _runtime.TryLoadCsvFromFile(path);
            _status.text = loaded ? "CSV loaded into Vault specs" : "CSV not loaded";
            RefreshSliders();
        }
    }
}
#endif
